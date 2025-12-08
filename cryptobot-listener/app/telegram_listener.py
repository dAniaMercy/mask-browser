"""Telethon client for listening to CryptoBot messages"""
import re
import logging
import asyncio
from decimal import Decimal
from datetime import datetime
from telethon import TelegramClient, events
from telethon.tl.types import User as TelegramUser
from app.config import config
from app.database import Database
from app.redis_service import RedisService
from app.payment_processor import PaymentProcessor

logger = logging.getLogger(__name__)


def parse_cryptobot_message(text: str) -> dict | None:
    """
    Парсит сообщение от @CryptoBot
    Возвращает: {
        'sender_name': str,
        'invoice_id': str,
        'amount': Decimal,
        'currency': str,
        'usd_amount': Decimal
    }
    """
    # Паттерн для сообщения вида:
    # let name="Данич 🎁🎭🎁  "
    # оплатил(а) ваш счёт #IV39234014. Вы получили 🟢 5 USDT ($5).
    pattern = r'let name="([^"]+)".*?оплатил\(а\) ваш счёт #(\w+)\. Вы получили.*?([\d.]+)\s+(\w+)\s+\(\$?([\d.]+)\)'
    match = re.search(pattern, text, re.DOTALL | re.IGNORECASE)
    
    if match:
        try:
            return {
                'sender_name': match.group(1).strip(),
                'invoice_id': match.group(2),
                'amount': Decimal(match.group(3)),
                'currency': match.group(4).upper(),
                'usd_amount': Decimal(match.group(5))
            }
        except Exception as e:
            logger.error(f"Error parsing payment data: {e}")
            return None
    return None


def extract_payment_code(text: str) -> str | None:
    """Ищет код оплаты в формате MASK-XXXXXX"""
    pattern = r'MASK-([A-Z0-9]{6,12})'
    match = re.search(pattern, text, re.IGNORECASE)
    return match.group(0).upper() if match else None


class CryptoBotListener:
    """Telethon listener for CryptoBot payment notifications"""
    
    def __init__(self, db: Database, redis: RedisService, processor: PaymentProcessor):
        self.db = db
        self.redis = redis
        self.processor = processor
        self.client = None
        
    async def start(self):
        """Start Telegram client and begin listening"""
        if not config.TELEGRAM_API_ID or not config.TELEGRAM_API_HASH:
            raise ValueError("TELEGRAM_API_ID and TELEGRAM_API_HASH must be set")
        
        self.client = TelegramClient(
            f"{config.SESSION_DIR}/cryptobot_session",
            int(config.TELEGRAM_API_ID),
            config.TELEGRAM_API_HASH
        )
        
        await self.client.start(phone=config.TELEGRAM_PHONE)
        
        # Register event handler
        @self.client.on(events.NewMessage(from_users='CryptoBot'))
        async def handle_cryptobot_message(event):
            await self.process_message(event)
        
        logger.info("CryptoBot listener started and listening for messages")
        
        # Keep running
        await self.client.run_until_disconnected()
    
    async def stop(self):
        """Stop Telegram client"""
        if self.client:
            await self.client.disconnect()
            logger.info("CryptoBot listener stopped")
    
    async def process_message(self, event):
        """Process incoming message from CryptoBot"""
        try:
            message_text = event.message.message
            
            logger.debug(f"Received message from CryptoBot: {message_text[:200]}")
            
            # 1. Parse payment data
            payment_data = parse_cryptobot_message(message_text)
            if not payment_data:
                logger.debug("Message does not match payment pattern, skipping")
                return
            
            # 2. Extract payment code
            payment_code = extract_payment_code(message_text)
            
            # If code not found in main message, check reply
            if not payment_code and event.message.reply_to:
                try:
                    reply_msg = await event.get_reply_message()
                    if reply_msg:
                        payment_code = extract_payment_code(reply_msg.message)
                except Exception as e:
                    logger.warning(f"Error getting reply message: {e}")
            
            if not payment_code:
                logger.warning(f"No payment code found in message: {message_text[:100]}")
                return
            
            logger.info(f"Processing payment: code={payment_code}, invoice={payment_data['invoice_id']}")
            
            # 3. Check deduplication
            dedup_key = f"payment:{payment_data['invoice_id']}"
            if await self.redis.exists(dedup_key):
                logger.info(f"Duplicate payment ignored: {payment_data['invoice_id']}")
                return
            
            # 4. Find deposit request
            deposit = await self.db.get_pending_deposit_by_code(payment_code)
            if not deposit:
                logger.warning(f"No pending deposit found for code: {payment_code}")
                return
            
            # 5. Check expiration
            expires_at = deposit['expires_at']
            if isinstance(expires_at, str):
                expires_at = datetime.fromisoformat(expires_at.replace('Z', '+00:00'))
            
            if expires_at < datetime.utcnow().replace(tzinfo=expires_at.tzinfo):
                logger.warning(f"Deposit expired: {payment_code}")
                await self.db.update_deposit_status(deposit['id'], 'expired')
                return
            
            # 6. Process payment
            await self.processor.process_payment(
                deposit,
                payment_data,
                message_text
            )
            
            # 7. Mark as processed (TTL 24 hours)
            await self.redis.setex(dedup_key, 86400, '1')
            
        except Exception as e:
            logger.error(f"Error processing CryptoBot message: {e}", exc_info=True)

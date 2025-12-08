"""Notification service for webhook calls"""
import aiohttp
import hmac
import hashlib
import json
import logging
from datetime import datetime
from decimal import Decimal
from app.config import config

logger = logging.getLogger(__name__)


class NotificationService:
    """Send webhook notifications to main API"""
    
    def sign_webhook(self, payload: dict) -> str:
        """Sign webhook payload with HMAC SHA256"""
        message = json.dumps(payload, sort_keys=True)
        signature = hmac.new(
            config.WEBHOOK_SECRET.encode(),
            message.encode(),
            hashlib.sha256
        ).hexdigest()
        return signature
    
    async def notify_main_api(
        self,
        deposit_id: int,
        user_id: int,
        amount: Decimal,
        currency: str,
        transaction_id: str
    ):
        """Notify main API about completed deposit"""
        payload = {
            'event': 'deposit.completed',
            'deposit_id': deposit_id,
            'user_id': user_id,
            'amount': str(amount),
            'currency': currency,
            'transaction_id': transaction_id,
            'timestamp': datetime.utcnow().isoformat()
        }
        
        signature = self.sign_webhook(payload)
        
        try:
            async with aiohttp.ClientSession() as session:
                async with session.post(
                    config.MAIN_API_WEBHOOK_URL,
                    json=payload,
                    headers={
                        'X-Webhook-Signature': signature,
                        'Content-Type': 'application/json'
                    },
                    timeout=aiohttp.ClientTimeout(total=10)
                ) as response:
                    if response.status == 200:
                        logger.info(f"Webhook notification sent successfully for deposit {deposit_id}")
                    else:
                        text = await response.text()
                        logger.warning(
                            f"Webhook notification failed: status={response.status}, "
                            f"response={text[:200]}"
                        )
        except Exception as e:
            logger.error(f"Error sending webhook notification: {e}", exc_info=True)
            # Don't raise - webhook failure shouldn't fail payment processing

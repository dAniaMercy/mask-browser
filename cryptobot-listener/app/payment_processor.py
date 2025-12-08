"""Payment processing logic"""
import logging
import aiohttp
import hmac
import hashlib
import json
from decimal import Decimal
from datetime import datetime
from app.config import config
from app.database import Database
from app.services.notification import NotificationService

logger = logging.getLogger(__name__)


class PaymentProcessor:
    """Process completed payments"""
    
    def __init__(self, db: Database, notification: NotificationService):
        self.db = db
        self.notification = notification
    
    async def process_payment(self, deposit: dict, payment_data: dict, raw_message: str):
        """Process successful payment"""
        try:
            deposit_id = deposit['id']
            actual_amount = payment_data['usd_amount']
            transaction_id = payment_data['invoice_id']
            
            processor_response = {
                'raw': raw_message,
                'parsed': payment_data,
                'processed_at': datetime.utcnow().isoformat()
            }
            
            # Complete deposit in database
            await self.db.complete_deposit(
                deposit_id=deposit_id,
                actual_amount=actual_amount,
                transaction_id=transaction_id,
                processor_response=processor_response
            )
            
            # Notify main API via webhook
            await self.notification.notify_main_api(
                deposit_id=deposit_id,
                user_id=deposit['user_id'],
                amount=actual_amount,
                currency=payment_data['currency'],
                transaction_id=transaction_id
            )
            
            logger.info(
                f"Payment processed successfully: deposit_id={deposit_id}, "
                f"amount={actual_amount}, transaction={transaction_id}"
            )
            
        except Exception as e:
            logger.error(f"Error processing payment: {e}", exc_info=True)
            raise

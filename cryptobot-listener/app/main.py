"""Main entry point for CryptoBot Listener"""
import asyncio
import logging
import sys
from app.config import config
from app.database import Database
from app.redis_service import RedisService
from app.telegram_listener import CryptoBotListener
from app.payment_processor import PaymentProcessor
from app.services.notification import NotificationService

# Configure logging
logging.basicConfig(
    level=getattr(logging, config.LOG_LEVEL),
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout)
    ]
)

logger = logging.getLogger(__name__)


async def main():
    """Main application entry point"""
    db = Database()
    redis = RedisService()
    notification = NotificationService()
    processor = PaymentProcessor(db, notification)
    listener = CryptoBotListener(db, redis, processor)
    
    try:
        logger.info("Starting CryptoBot Listener...")
        
        # Connect to services
        await db.connect()
        await redis.connect()
        
        # Start listener (this will run until disconnected)
        await listener.start()
        
    except KeyboardInterrupt:
        logger.info("Received shutdown signal")
    except Exception as e:
        logger.error(f"Fatal error: {e}", exc_info=True)
        sys.exit(1)
    finally:
        # Cleanup
        await listener.stop()
        await redis.close()
        await db.close()
        logger.info("CryptoBot Listener stopped")


if __name__ == "__main__":
    asyncio.run(main())

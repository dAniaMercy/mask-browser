"""Redis service for deduplication and caching"""
try:
    import redis.asyncio as redis
except ImportError:
    # Fallback for older redis versions
    import aioredis as redis
import logging
from app.config import config

logger = logging.getLogger(__name__)


class RedisService:
    """Redis operations"""
    
    def __init__(self):
        self.client: redis.Redis | None = None
    
    async def connect(self):
        """Connect to Redis"""
        try:
            self.client = await redis.from_url(
                config.REDIS_URL,
                encoding="utf-8",
                decode_responses=True
            )
            # Test connection
            await self.client.ping()
            logger.info("Connected to Redis")
        except Exception as e:
            logger.error(f"Failed to connect to Redis: {e}")
            raise
    
    async def close(self):
        """Close Redis connection"""
        if self.client:
            await self.client.close()
            logger.info("Redis connection closed")
    
    async def exists(self, key: str) -> bool:
        """Check if key exists"""
        if not self.client:
            return False
        return await self.client.exists(key) > 0
    
    async def setex(self, key: str, seconds: int, value: str):
        """Set key with expiration"""
        if not self.client:
            return
        await self.client.setex(key, seconds, value)
    
    async def get(self, key: str) -> str | None:
        """Get value by key"""
        if not self.client:
            return None
        return await self.client.get(key)

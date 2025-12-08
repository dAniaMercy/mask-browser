"""Database connection and queries"""
import asyncpg
import logging
from typing import Optional
from decimal import Decimal
from datetime import datetime
from app.config import config

logger = logging.getLogger(__name__)


class Database:
    """Database operations for deposit requests"""
    
    def __init__(self):
        self.pool: Optional[asyncpg.Pool] = None
    
    async def connect(self):
        """Initialize connection pool"""
        try:
            self.pool = await asyncpg.create_pool(
                config.DATABASE_URL,
                min_size=2,
                max_size=10,
                command_timeout=60
            )
            logger.info("Database connection pool created")
        except Exception as e:
            logger.error(f"Failed to create database pool: {e}")
            raise
    
    async def close(self):
        """Close connection pool"""
        if self.pool:
            await self.pool.close()
            logger.info("Database connection pool closed")
    
    async def get_pending_deposit_by_code(self, payment_code: str) -> Optional[dict]:
        """Find pending deposit request by payment code"""
        async with self.pool.acquire() as conn:
            row = await conn.fetchrow(
                """
                SELECT id, user_id, payment_code, expected_amount, currency,
                       payment_method_id, status, expires_at, created_at
                FROM deposit_requests
                WHERE payment_code = $1 AND status = 'pending'
                """,
                payment_code
            )
            
            if row:
                return dict(row)
            return None
    
    async def update_deposit_status(self, deposit_id: int, status: str):
        """Update deposit request status"""
        async with self.pool.acquire() as conn:
            await conn.execute(
                """
                UPDATE deposit_requests
                SET status = $1
                WHERE id = $2
                """,
                status, deposit_id
            )
            logger.info(f"Deposit {deposit_id} status updated to {status}")
    
    async def complete_deposit(
        self,
        deposit_id: int,
        actual_amount: Decimal,
        transaction_id: str,
        processor_response: dict
    ):
        """Complete deposit and update user balance"""
        import json
        
        async with self.pool.acquire() as conn:
            async with conn.transaction():
                # Update deposit request
                await conn.execute(
                    """
                    UPDATE deposit_requests
                    SET status = 'completed',
                        actual_amount = $1,
                        transaction_id = $2,
                        processor_response = $3,
                        completed_at = NOW()
                    WHERE id = $4
                    """,
                    actual_amount, transaction_id, json.dumps(processor_response), deposit_id
                )
                
                # Get user_id
                user_id = await conn.fetchval(
                    "SELECT user_id FROM deposit_requests WHERE id = $1",
                    deposit_id
                )
                
                # Update user balance
                await conn.execute(
                    """
                    UPDATE users
                    SET balance = balance + $1
                    WHERE id = $2
                    """,
                    actual_amount, user_id
                )
                
                # Create payment record
                await conn.execute(
                    """
                    INSERT INTO payments (
                        user_id, amount, currency, provider, transaction_id,
                        status, completed_at, deposit_request_id, payment_method_id,
                        processor_transaction_id, processor_response, created_at
                    )
                    VALUES (
                        $1, $2, 
                        (SELECT currency FROM deposit_requests WHERE id = $6),
                        0, $3, 1, NOW(), $6,
                        (SELECT payment_method_id FROM deposit_requests WHERE id = $6),
                        $3, $4, NOW()
                    )
                    """,
                    user_id, actual_amount, transaction_id,
                    json.dumps(processor_response), deposit_id, deposit_id
                )
                
                logger.info(
                    f"Deposit {deposit_id} completed: {actual_amount} for user {user_id}"
                )

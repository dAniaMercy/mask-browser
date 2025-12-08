"""Configuration management for CryptoBot Listener"""
import os
from pydantic_settings import BaseSettings
from typing import Optional


class Config(BaseSettings):
    """Application configuration"""
    
    # Database
    DATABASE_URL: str = os.getenv(
        "DATABASE_URL",
        "postgresql://maskuser:maskpass123@postgres:5432/maskbrowser"
    )
    
    # Redis
    REDIS_URL: str = os.getenv("REDIS_URL", "redis://redis:6379")
    
    # Telegram
    TELEGRAM_API_ID: Optional[str] = os.getenv("TELEGRAM_API_ID")
    TELEGRAM_API_HASH: Optional[str] = os.getenv("TELEGRAM_API_HASH")
    TELEGRAM_PHONE: Optional[str] = os.getenv("TELEGRAM_PHONE")
    
    # CryptoBot API (optional)
    CRYPTOBOT_API_TOKEN: Optional[str] = os.getenv("CRYPTOBOT_API_TOKEN")
    
    # Main API Webhook
    MAIN_API_WEBHOOK_URL: str = os.getenv(
        "MAIN_API_WEBHOOK_URL",
        "http://api:8080/api/deposit/webhook/cryptobot"
    )
    WEBHOOK_SECRET: str = os.getenv("WEBHOOK_SECRET", "super_secret_webhook")
    
    # Session storage
    SESSION_DIR: str = os.getenv("SESSION_DIR", "/app/sessions")
    
    # Logging
    LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")
    
    class Config:
        env_file = ".env"
        case_sensitive = True


config = Config()

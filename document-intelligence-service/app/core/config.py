"""Application configuration and settings."""

import os
from typing import Optional, Set
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Runtime configuration loaded from environment variables."""

    tesseract_cmd: Optional[str] = None
    ocr_default_language: str = "sin+eng"
    ocr_max_upload_mb: int = 25
    ocr_pdf_dpi: int = 300
    log_level: str = "INFO"

    allowed_media_types: Set[str] = {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/jpg",
    }

    supported_language_modes: Set[str] = {
        "eng",
        "sin",
        "sin+eng",
    }

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
        case_sensitive=False,
    )

    @property
    def max_upload_bytes(self) -> int:
        return self.ocr_max_upload_mb * 1024 * 1024


settings = Settings()

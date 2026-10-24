"""Application configuration and settings."""

import os
from pathlib import Path
from typing import Optional, Set
from pydantic import field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

# Resolve service root directory: .../document-intelligence-service
_SERVICE_ROOT = Path(__file__).resolve().parent.parent.parent
_ENV_FILE = _SERVICE_ROOT / ".env"


class Settings(BaseSettings):
    """Runtime configuration loaded from environment variables and service .env."""

    environment: str = "development"
    tesseract_cmd: Optional[str] = None
    ocr_default_language: str = "sin"
    ocr_max_upload_mb: int = 25
    ocr_pdf_dpi: int = 300
    log_level: str = "INFO"
    # Semantic Extraction / Model Provider Settings
    semantic_provider: str = "gemini"
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "qwen3:1.7b"
    ollama_timeout_seconds: float = 60.0
    ollama_think: bool = False
    ollama_temperature: float = 0.0
    ollama_keep_alive: str = "5m"
    semantic_prompt_version: str = "qwen-proposal-extraction-v5"
    run_qwen_live_tests: bool = False
    semantic_max_input_length: int = 3000

    # Gemini Settings (used when semantic_provider="gemini")
    gemini_model: str = "gemini-flash-latest"
    gemini_api_key: Optional[str] = None
    gemini_timeout_seconds: float = 60.0
    gemini_base_url: str = "https://generativelanguage.googleapis.com/v1beta"

    @field_validator("gemini_api_key", mode="before")
    @classmethod
    def clean_gemini_api_key(cls, v: Optional[str]) -> Optional[str]:
        """Strips surrounding quotes and outer whitespace; returns None if empty."""
        if v is None:
            return None
        v_str = str(v).strip()
        if (v_str.startswith('"') and v_str.endswith('"')) or (v_str.startswith("'") and v_str.endswith("'")):
            v_str = v_str[1:-1].strip()
        return v_str if v_str else None

    @property
    def gemini_api_key_source(self) -> str:
        """
        Returns the configuration source of GEMINI_API_KEY without exposing its value:
        - 'os.environ' if set in the process environment.
        - '.env' if loaded from the service root .env file.
        - 'unconfigured' if empty or not set.
        """
        env_val = os.getenv("GEMINI_API_KEY")
        if env_val is not None and env_val.strip():
            return "os.environ"
        if self.gemini_api_key and self.gemini_api_key.strip():
            return ".env"
        return "unconfigured"

    @model_validator(mode="after")
    def resolve_timeouts(self) -> "Settings":
        """
        Resolve configuration-driven timeouts.
        Development/local inference defaults to 240.0s per chat request to allow multi-page
        chunk extraction without false timeouts.
        Production environment defaults to conservative 60.0s baseline.
        """
        if "ollama_timeout_seconds" not in self.model_fields_set:
            if self.environment.lower() in ("development", "dev", "local"):
                self.ollama_timeout_seconds = 240.0
            else:
                self.ollama_timeout_seconds = 60.0

        if self.ollama_timeout_seconds <= 0:
            raise ValueError("ollama_timeout_seconds must be greater than 0.")

        return self

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
        env_file=_ENV_FILE if _ENV_FILE.exists() else None,
        env_file_encoding="utf-8",
        extra="ignore",
        case_sensitive=False,
    )

    @property
    def max_upload_bytes(self) -> int:
        return self.ocr_max_upload_mb * 1024 * 1024


settings = Settings()


def get_gemini_api_key_source() -> str:
    """Returns the configuration source of GEMINI_API_KEY without exposing its value."""
    return settings.gemini_api_key_source

"""Reproducibility metadata capture and audit reporting for document intelligence evaluation."""

import datetime
import os
import platform
import subprocess
from dataclasses import asdict, dataclass
from typing import Any, Dict, Optional

from app.semantic.ollama_client import OllamaClient


@dataclass(frozen=True)
class ReproducibilityMetadata:
    """Non-secret experiment provenance metadata."""

    timestamp_utc: str
    model_identifier: str
    prompt_version: str
    ocr_preprocessing: bool
    language_mode: str
    dataset_split: str
    python_version: str
    operating_system: str
    temperature: float = 0.0
    think_disabled: bool = True
    schema_version: str = "v2"
    ollama_version: Optional[str] = None
    model_digest: Optional[str] = None
    git_commit_hash: Optional[str] = None

    @classmethod
    def capture(
        cls,
        model_identifier: str,
        prompt_version: str,
        ocr_preprocessing: bool,
        language_mode: str,
        dataset_split: str,
        temperature: float = 0.0,
        think_disabled: bool = True,
        schema_version: str = "v2",
    ) -> "ReproducibilityMetadata":
        git_hash = None
        try:
            res = subprocess.run(
                ["git", "rev-parse", "HEAD"],
                capture_output=True,
                text=True,
                check=False,
                timeout=5,
            )
            if res.returncode == 0:
                git_hash = res.stdout.strip()[:10]
        except Exception:
            git_hash = None

        ollama_ver = None
        model_digest = None
        try:
            client = OllamaClient()
            ollama_ver = client.get_version()
            model_digest = client.get_model_digest(model_identifier)
        except Exception:
            pass

        return cls(
            timestamp_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
            model_identifier=model_identifier,
            prompt_version=prompt_version,
            ocr_preprocessing=ocr_preprocessing,
            language_mode=language_mode,
            dataset_split=dataset_split,
            python_version=platform.python_version(),
            operating_system=f"{platform.system()} {platform.release()}",
            temperature=temperature,
            think_disabled=think_disabled,
            schema_version=schema_version,
            ollama_version=ollama_ver,
            model_digest=model_digest,
            git_commit_hash=git_hash,
        )

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)

"""HTTP client for local Ollama service integration."""

import json
from typing import Any, Dict, List, Optional
import httpx

from app.core.config import settings
from app.core.logging import logger


class OllamaError(Exception):
    """Base exception for Ollama client errors."""
    pass


class OllamaConnectionError(OllamaError):
    """Raised when Ollama HTTP daemon is unreachable."""
    pass


class OllamaModelNotFoundError(OllamaError):
    """Raised when the requested Ollama model is not installed locally."""
    pass


class OllamaTimeoutError(OllamaError):
    """Raised when an Ollama inference request times out."""
    pass


class OllamaResponseError(OllamaError):
    """Raised when Ollama returns an error or invalid HTTP response."""
    pass


class OllamaClient:
    """
    Dedicated HTTP client for Ollama API interaction.
    Never executes shell commands ('ollama run'). Uses pure HTTP requests.
    """

    def __init__(
        self,
        base_url: Optional[str] = None,
        timeout_seconds: Optional[float] = None,
    ):
        self.base_url = (base_url or settings.ollama_base_url).rstrip("/")
        self.timeout_seconds = timeout_seconds if timeout_seconds is not None else settings.ollama_timeout_seconds

    def check_health(self) -> bool:
        """Check if Ollama service is reachable and responsive."""
        try:
            with httpx.Client(timeout=5.0) as client:
                res = client.get(f"{self.base_url}/api/tags")
                return res.status_code == 200
        except Exception as ex:
            logger.debug(f"Ollama health check failed: {ex}")
            return False

    def get_version(self) -> Optional[str]:
        """Retrieve Ollama service version if available from /api/version."""
        try:
            with httpx.Client(timeout=5.0) as client:
                res = client.get(f"{self.base_url}/api/version")
                if res.status_code == 200:
                    data = res.json()
                    return data.get("version")
        except Exception:
            pass
        return None

    def list_models(self) -> List[Dict[str, Any]]:
        """List all models currently installed locally in Ollama."""
        try:
            with httpx.Client(timeout=10.0) as client:
                res = client.get(f"{self.base_url}/api/tags")
                if res.status_code != 200:
                    raise OllamaResponseError(
                        f"Failed to list Ollama models. HTTP {res.status_code}: {res.text}"
                    )
                data = res.json()
                return data.get("models", [])
        except httpx.ConnectError as ex:
            raise OllamaConnectionError(
                f"Cannot connect to Ollama at {self.base_url}. Ensure Ollama daemon is running."
            ) from ex
        except httpx.TimeoutException as ex:
            raise OllamaTimeoutError(f"Ollama request timed out while listing models.") from ex
        except Exception as ex:
            if isinstance(ex, OllamaError):
                raise
            raise OllamaError(f"Unexpected error communicating with Ollama: {ex}") from ex

    def get_model_info(self, model_name: str) -> Optional[Dict[str, Any]]:
        """
        Check if a given model is installed, returning its metadata dict or None.
        Matches exact tag or tag without :latest suffix if appropriate.
        """
        models = self.list_models()
        target = model_name.strip().lower()
        target_base = target.split(":")[0] if ":" in target else target

        for m in models:
            m_name = m.get("name", "").strip().lower()
            m_model = m.get("model", "").strip().lower()
            if m_name == target or m_model == target:
                return m
            # If target has no tag, match base:latest or first match
            if ":" not in target and (m_name == f"{target}:latest" or m_model == f"{target}:latest"):
                return m
            if m_name.split(":")[0] == target_base and m_name == target:
                return m

        return None

    def get_model_digest(self, model_name: str) -> Optional[str]:
        """Obtain sha256 model digest for evaluation reproducibility."""
        info = self.get_model_info(model_name)
        if info:
            return info.get("digest")
        return None

    def chat(
        self,
        model: str,
        messages: List[Dict[str, str]],
        schema: Optional[Dict[str, Any]] = None,
        think: bool = False,
        temperature: float = 0.0,
        keep_alive: str = "5m",
    ) -> Dict[str, Any]:
        """
        Execute chat completion in Ollama with thinking disabled and optional schema constraints.

        Governance Invariants:
        1. think=False is strictly enforced: reasoning traces are never generated or persisted.
        2. stream=False: completes deterministically in single response.
        3. temperature=0.0: greedy deterministic generation.
        """
        payload: Dict[str, Any] = {
            "model": model,
            "messages": messages,
            "stream": False,
            "think": think,
            "options": {
                "temperature": temperature,
                "num_predict": 500,
                "repeat_penalty": 1.15,
            },
            "keep_alive": keep_alive,
        }

        if schema is not None:
            payload["format"] = schema
        else:
            payload["format"] = "json"

        try:
            with httpx.Client(timeout=self.timeout_seconds) as client:
                res = client.post(
                    f"{self.base_url}/api/chat",
                    json=payload,
                )
                if res.status_code == 404:
                    raise OllamaModelNotFoundError(
                        f"Configured Ollama model '{model}' is not installed. Run: ollama pull {model}"
                    )
                if res.status_code != 200:
                    raise OllamaResponseError(
                        f"Ollama chat failed with HTTP {res.status_code}: {res.text}"
                    )
                return res.json()
        except httpx.ConnectError as ex:
            raise OllamaConnectionError(
                f"Cannot connect to Ollama at {self.base_url}. Ensure Ollama daemon is running."
            ) from ex
        except httpx.TimeoutException as ex:
            raise OllamaTimeoutError(
                f"Ollama chat request timed out after {self.timeout_seconds}s for model '{model}'."
            ) from ex
        except Exception as ex:
            if isinstance(ex, OllamaError):
                raise
            raise OllamaError(f"Unexpected error communicating with Ollama: {ex}") from ex


"""
Google Gemini HTTP API Client for Structured Proposal Semantic Extraction.

Uses direct HTTP (httpx) to communicate with the Google Generative AI REST API.
Enforces strict security invariants:
- Never hardcodes or logs API keys.
- Reads GEMINI_API_KEY from environment or configuration.
- Transmits API key securely via 'x-goog-api-key' request header.
- Provides clean exception handling for authentication, rate limits, timeouts, and server errors.
"""

import json
import logging
import os
import random
import time
from typing import Any, Dict, List, Optional
import httpx

from app.core.config import settings

logger = logging.getLogger(__name__)


# ============================================================================
# EXCEPTION HIERARCHY
# ============================================================================

class GeminiError(Exception):
    """Base exception for all Gemini client failures."""
    pass


class GeminiAuthenticationError(GeminiError):
    """Raised when GEMINI_API_KEY is missing, empty, or rejected with HTTP 401/403."""
    pass


class GeminiRateLimitError(GeminiError):
    """Raised when Gemini returns HTTP 429 (Resource Exhausted / Rate Limit)."""
    pass


class GeminiTimeoutError(GeminiError):
    """Raised when an HTTP request to the Gemini API times out."""
    pass


class GeminiServerError(GeminiError):
    """Raised when Gemini returns an HTTP 5xx server-side failure."""
    pass


class GeminiResponseError(GeminiError):
    """Raised when Gemini returns an unexpected or malformed response structure."""
    pass


class GeminiBadRequestError(GeminiError):
    """Raised when Gemini returns HTTP 400 Bad Request (client error, non-retryable)."""
    pass


class GeminiNotFoundError(GeminiError):
    """Raised when Gemini returns HTTP 404 Not Found (e.g. invalid model, non-retryable)."""
    pass


# ============================================================================
# GEMINI CLIENT
# ============================================================================

class GeminiClient:
    """Client for Google Gemini REST API using structured output generation with bounded retries."""

    def __init__(
        self,
        api_key: Optional[str] = None,
        model_name: Optional[str] = None,
        timeout_seconds: Optional[float] = None,
        base_url: Optional[str] = None,
    ):
        # Resolve API key strictly from parameter or environment, stripping quotes and whitespace
        raw_key = api_key if api_key is not None else (os.getenv("GEMINI_API_KEY") or settings.gemini_api_key)
        if raw_key:
            clean_key = str(raw_key).strip()
            if (clean_key.startswith('"') and clean_key.endswith('"')) or (clean_key.startswith("'") and clean_key.endswith("'")):
                clean_key = clean_key[1:-1].strip()
            if any(ord(c) < 32 or ord(c) == 127 for c in clean_key):
                raise GeminiAuthenticationError(
                    "GEMINI_API_KEY contains invalid control characters (e.g. unprintable bytes)."
                )
            self._api_key = clean_key if clean_key else None
        else:
            self._api_key = None

        raw_model = model_name or os.getenv("GEMINI_MODEL") or settings.gemini_model
        if raw_model:
            clean_model = str(raw_model).strip()
            if (clean_model.startswith('"') and clean_model.endswith('"')) or (clean_model.startswith("'") and clean_model.endswith("'")):
                clean_model = clean_model[1:-1].strip()
            if clean_model.startswith("models/"):
                clean_model = clean_model.removeprefix("models/").strip()
            self.model_name = clean_model
        else:
            self.model_name = "gemini-flash-latest"
        self.timeout_seconds = timeout_seconds if timeout_seconds is not None else settings.gemini_timeout_seconds
        self.base_url = (base_url or settings.gemini_base_url).rstrip("/")
        self.last_execution_stats: Dict[str, Any] = {}
        self.last_raw_http_response_text: Optional[str] = None
        self.last_raw_response_text: Optional[str] = None
        self.last_parsed_response: Optional[Dict[str, Any]] = None

    @property
    def is_configured(self) -> bool:
        """Returns True if a valid non-empty API key without control characters is available."""
        if not self._api_key or not self._api_key.strip():
            return False
        if any(ord(c) < 32 or ord(c) == 127 for c in self._api_key):
            return False
        return True

    def _validate_api_key(self) -> None:
        """
        Pre-flight validation before any HTTP request.
        Rejects unconfigured, empty, whitespace-only credentials, and credentials
        containing invalid control characters.
        Never reveals the credential value in errors or logs.
        """
        if not self._api_key or not self._api_key.strip():
            raise GeminiAuthenticationError(
                "GEMINI_API_KEY environment variable is not configured. "
                "Cloud semantic extraction requires a valid Gemini API key in .env or environment."
            )
        if any(ord(c) < 32 or ord(c) == 127 for c in self._api_key):
            raise GeminiAuthenticationError(
                "GEMINI_API_KEY contains invalid control characters (e.g. carriage returns or unprintable bytes)."
            )

    def list_models(self) -> List[str]:
        """Queries Google Generative AI models endpoint to list available model names."""
        self._validate_api_key()
        endpoint_url = f"{self.base_url}/models"
        headers = {"x-goog-api-key": self._api_key}
        try:
            with httpx.Client(timeout=self.timeout_seconds) as client:
                resp = client.get(endpoint_url, headers=headers)
        except httpx.RequestError as ex:
            raise GeminiError(f"Failed to query Gemini models endpoint: {ex}") from ex

        if resp.status_code in (401, 403):
            raise GeminiAuthenticationError(f"Gemini authentication failed (HTTP {resp.status_code}).")
        if resp.status_code == 400:
            raise GeminiBadRequestError(f"Gemini API bad request (HTTP 400): {resp.text[:200]}")
        if resp.status_code != 200:
            raise GeminiServerError(f"Failed to list models (HTTP {resp.status_code}): {resp.text[:200]}")
        try:
            data = resp.json()
            return [m.get("name", "") for m in data.get("models", [])]
        except Exception as ex:
            raise GeminiResponseError(f"Failed to parse models response: {ex}") from ex

    def verify_model_availability(self, model_name: Optional[str] = None) -> bool:
        """Checks if the configured or target model is available for the current credentials."""
        target = model_name or self.model_name
        try:
            models = self.list_models()
            return f"models/{target}" in models or target in models
        except Exception as ex:
            logger.warning(f"Could not verify model availability: {ex}")
            return False

    def generate_content(
        self,
        prompt: str,
        system_instruction: Optional[str] = None,
        schema: Optional[Dict[str, Any]] = None,
        temperature: float = 0.0,
        max_attempts: int = 3,
    ) -> Dict[str, Any]:
        """
        Invokes Gemini generateContent with JSON structured output schema and bounded retries.
        
        Security Invariant: The API key is sent via header and NEVER logged.
        """
        self._validate_api_key()

        endpoint_url = f"{self.base_url}/models/{self.model_name}:generateContent"

        # Construct request body adhering to Gemini API specification
        generation_config: Dict[str, Any] = {
            "temperature": temperature,
            "responseMimeType": "application/json",
        }
        if schema:
            generation_config["responseSchema"] = schema

        payload: Dict[str, Any] = {
            "contents": [
                {
                    "role": "user",
                    "parts": [{"text": prompt}],
                }
            ],
            "generationConfig": generation_config,
        }

        if system_instruction:
            payload["systemInstruction"] = {
                "parts": [{"text": system_instruction}]
            }

        headers = {
            "Content-Type": "application/json",
            "x-goog-api-key": self._api_key,
        }

        start_time = time.perf_counter()
        total_backoff_duration = 0.0
        last_exception: Optional[Exception] = None
        last_status_code: Optional[int] = None
        response: Optional[httpx.Response] = None
        successful_attempt_duration: Optional[float] = None

        for attempt in range(1, max_attempts + 1):
            attempt_start = time.perf_counter()
            logger.info(
                f"Invoking Gemini API (attempt {attempt}/{max_attempts}): model='{self.model_name}', timeout={self.timeout_seconds}s"
            )
            response = None
            try:
                with httpx.Client(timeout=self.timeout_seconds) as client:
                    response = client.post(endpoint_url, json=payload, headers=headers)

                self.last_raw_http_response_text = response.text if response is not None else None
                last_status_code = response.status_code

                # Handle Authentication error: DO NOT RETRY
                if response.status_code in (401, 403):
                    logger.error(f"Gemini API authentication failed (HTTP {response.status_code})")
                    wall_elapsed = round(time.perf_counter() - start_time, 3)
                    self.last_execution_stats = {
                        "attempts_made": attempt,
                        "last_status_code": response.status_code,
                        "last_error_message": f"HTTP {response.status_code} Authentication Failed",
                        "elapsed_seconds": wall_elapsed,
                        "total_wall_time_seconds": wall_elapsed,
                        "successful_attempt_duration_seconds": None,
                        "retry_backoff_duration_seconds": round(total_backoff_duration, 3),
                    }
                    raise GeminiAuthenticationError(
                        f"Gemini API authentication failed (HTTP {response.status_code}). Verify GEMINI_API_KEY."
                    )

                # Handle Bad Request (HTTP 400): Client error, DO NOT RETRY
                elif response.status_code == 400:
                    err_msg = f"HTTP 400: {response.text[:300]}"
                    logger.error(f"Gemini API bad request (HTTP 400): {err_msg}")
                    wall_elapsed = round(time.perf_counter() - start_time, 3)
                    self.last_execution_stats = {
                        "attempts_made": attempt,
                        "last_status_code": 400,
                        "last_error_message": err_msg,
                        "elapsed_seconds": wall_elapsed,
                        "total_wall_time_seconds": wall_elapsed,
                        "successful_attempt_duration_seconds": None,
                        "retry_backoff_duration_seconds": round(total_backoff_duration, 3),
                    }
                    raise GeminiBadRequestError(err_msg)

                # Handle Not Found (HTTP 404): Client error, DO NOT RETRY
                elif response.status_code == 404:
                    err_msg = f"HTTP 404: {response.text[:300]}"
                    logger.error(f"Gemini API not found (HTTP 404): {err_msg}")
                    wall_elapsed = round(time.perf_counter() - start_time, 3)
                    self.last_execution_stats = {
                        "attempts_made": attempt,
                        "last_status_code": 404,
                        "last_error_message": err_msg,
                        "elapsed_seconds": wall_elapsed,
                        "total_wall_time_seconds": wall_elapsed,
                        "successful_attempt_duration_seconds": None,
                        "retry_backoff_duration_seconds": round(total_backoff_duration, 3),
                    }
                    raise GeminiNotFoundError(err_msg)

                # Handle Rate limit error: transient / retryable
                elif response.status_code == 429:
                    err_msg = f"HTTP 429 Rate Limit / Quota Exceeded: {response.text[:200]}"
                    logger.warning(f"Gemini API rate limit exceeded on attempt {attempt}: {err_msg}")
                    last_exception = GeminiRateLimitError(err_msg)

                # Handle Server error (503 / 5xx): transient / retryable
                elif response.status_code >= 500:
                    status_name = "UNAVAILABLE" if response.status_code == 503 else "SERVER_ERROR"
                    err_msg = f"HTTP {response.status_code} / {status_name}: {response.text[:200]}"
                    logger.warning(f"Gemini API server error on attempt {attempt}: {err_msg}")
                    last_exception = GeminiServerError(err_msg)

                elif response.status_code != 200:
                    err_msg = f"HTTP {response.status_code}: {response.text[:300]}"
                    logger.error(f"Gemini API non-200 response on attempt {attempt}: {err_msg}")
                    last_exception = GeminiError(err_msg)

                else:
                    # HTTP 200 OK -> success
                    successful_attempt_duration = round(time.perf_counter() - attempt_start, 3)
                    wall_elapsed = round(time.perf_counter() - start_time, 3)
                    self.last_execution_stats = {
                        "attempts_made": attempt,
                        "last_status_code": 200,
                        "last_error_message": None,
                        "elapsed_seconds": wall_elapsed,
                        "total_wall_time_seconds": wall_elapsed,
                        "successful_attempt_duration_seconds": successful_attempt_duration,
                        "retry_backoff_duration_seconds": round(total_backoff_duration, 3),
                    }
                    break

            except (httpx.TimeoutException, httpx.RequestError) as net_ex:
                if isinstance(net_ex, httpx.TimeoutException):
                    logger.warning(f"Gemini API request timed out on attempt {attempt} after {self.timeout_seconds}s")
                    last_exception = GeminiTimeoutError(f"Gemini API request timed out after {self.timeout_seconds}s")
                else:
                    logger.warning(f"Gemini API network connection failure on attempt {attempt}: {net_ex}")
                    last_exception = GeminiError(f"Gemini API network connection failure: {net_ex}")
                last_status_code = None

            # Prepare retry if attempts remaining
            if attempt < max_attempts:
                sleep_delay = None
                if response is not None and "retry-after" in response.headers:
                    try:
                        sleep_delay = float(response.headers["retry-after"])
                        sleep_delay = max(0.5, min(sleep_delay, 15.0))
                    except (ValueError, TypeError):
                        pass

                if sleep_delay is None:
                    # Exponential backoff with jitter (1.5s, 3.0s, ...)
                    sleep_delay = min(1.5 * (2 ** (attempt - 1)) + random.uniform(0.1, 0.5), 15.0)

                total_backoff_duration += sleep_delay
                logger.info(f"Retrying Gemini API in {sleep_delay:.2f}s (attempt {attempt + 1}/{max_attempts})...")
                time.sleep(sleep_delay)
            else:
                wall_elapsed = round(time.perf_counter() - start_time, 3)
                self.last_execution_stats = {
                    "attempts_made": attempt,
                    "last_status_code": last_status_code,
                    "last_error_message": str(last_exception),
                    "elapsed_seconds": wall_elapsed,
                    "total_wall_time_seconds": wall_elapsed,
                    "successful_attempt_duration_seconds": None,
                    "retry_backoff_duration_seconds": round(total_backoff_duration, 3),
                }
                logger.error(f"Gemini API exhausted {max_attempts} attempts. Last error: {last_exception}")
                raise last_exception

        # Parse response JSON
        if response is None or response.status_code != 200:
            raise GeminiResponseError("Gemini response is unavailable after retry loop.")

        try:
            resp_data = response.json()
        except Exception as ex:
            raise GeminiResponseError(f"Failed to parse Gemini response as JSON: {ex}") from ex

        candidates = resp_data.get("candidates", [])
        if not candidates:
            # Check prompt feedback (safety filter or block)
            feedback = resp_data.get("promptFeedback", {})
            block_reason = feedback.get("blockReason")
            if block_reason:
                raise GeminiResponseError(f"Gemini request was blocked by safety policy: {block_reason}")
            raise GeminiResponseError("Gemini returned an empty candidates list with no text.")

        candidate = candidates[0]
        content_parts = candidate.get("content", {}).get("parts", [])
        if not content_parts or "text" not in content_parts[0]:
            finish_reason = candidate.get("finishReason", "UNKNOWN")
            raise GeminiResponseError(f"Gemini candidate contains no text parts (finishReason: {finish_reason}).")

        raw_json_text = content_parts[0]["text"].strip()
        self.last_raw_response_text = raw_json_text

        # Parse structured output
        try:
            parsed_result = json.loads(raw_json_text)
            self.last_parsed_response = parsed_result
            return parsed_result
        except json.JSONDecodeError as ex:
            logger.warning(f"Gemini output was not valid JSON, attempting repair: {ex}")
            clean_text = raw_json_text
            if clean_text.startswith("```"):
                clean_text = clean_text.split("\n", 1)[-1]
                if clean_text.endswith("```"):
                    clean_text = clean_text.rsplit("```", 1)[0]
            try:
                parsed_result = json.loads(clean_text)
                self.last_parsed_response = parsed_result
                return parsed_result
            except Exception as repair_ex:
                raise GeminiResponseError(
                    f"Gemini output could not be parsed as JSON: {repair_ex}"
                ) from ex


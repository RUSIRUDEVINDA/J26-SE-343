"""Internal HTTP boundary for the validated governance complaint classifier.

The intended caller is the StateLandGovernance .NET backend. Development
startup binds to loopback; this module does not add CORS or authentication.
"""

from __future__ import annotations

import logging
import os
import sys
from contextlib import asynccontextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, StrictStr, field_validator

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

from predict_v1 import (
    DEFAULT_JOBLIB,
    DEFAULT_META,
    DEFAULT_VALIDATION,
    load_versioned_artifact,
    predict_complaint,
)

logger = logging.getLogger(__name__)

DEFAULT_MAX_REQUEST_CHARS = 10_000
MODEL_PATH_ENV = "GOVERNANCE_MODEL_PATH"
METADATA_PATH_ENV = "GOVERNANCE_MODEL_METADATA_PATH"
VALIDATION_PATH_ENV = "GOVERNANCE_MODEL_VALIDATION_PATH"
MAX_REQUEST_CHARS_ENV = "GOVERNANCE_MAX_REQUEST_CHARS"

ArtifactLoader = Callable[..., dict[str, Any]]
Predictor = Callable[[str, dict[str, Any], str | None], dict[str, Any]]


@dataclass(frozen=True)
class ServiceSettings:
    """Deployment configuration. Defaults are independent of the process CWD."""

    model_path: Path
    metadata_path: Path
    validation_path: Path
    max_request_chars: int = DEFAULT_MAX_REQUEST_CHARS

    @classmethod
    def from_environment(cls) -> "ServiceSettings":
        def configured_path(name: str, default: Path) -> Path:
            configured = os.getenv(name)
            return Path(configured).expanduser().resolve() if configured else default.resolve()

        raw_limit = os.getenv(MAX_REQUEST_CHARS_ENV, str(DEFAULT_MAX_REQUEST_CHARS))
        try:
            limit = int(raw_limit)
        except ValueError as exc:
            raise ValueError(f"{MAX_REQUEST_CHARS_ENV} must be a positive integer") from exc
        if limit <= 0:
            raise ValueError(f"{MAX_REQUEST_CHARS_ENV} must be a positive integer")

        return cls(
            model_path=configured_path(MODEL_PATH_ENV, DEFAULT_JOBLIB),
            metadata_path=configured_path(METADATA_PATH_ENV, DEFAULT_META),
            validation_path=configured_path(VALIDATION_PATH_ENV, DEFAULT_VALIDATION),
            max_request_chars=limit,
        )


class PredictionRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    complaint_text: StrictStr
    case_id: StrictStr | None = None

    @field_validator("complaint_text")
    @classmethod
    def complaint_text_must_not_be_blank(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("complaint_text must not be blank")
        return value


class PredictionResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    model_version: str
    predicted_category: str
    class_probabilities: dict[str, float]
    case_id: str | None
    advisory_note: str
    closed_set_note: str


class HealthResponse(BaseModel):
    status: str
    model_version: str


def create_app(
    settings: ServiceSettings | None = None,
    *,
    artifact_loader: ArtifactLoader = load_versioned_artifact,
    predictor: Predictor = predict_complaint,
) -> FastAPI:
    """Create an app whose validated model is loaded exactly once at startup."""
    resolved_settings = settings or ServiceSettings.from_environment()

    @asynccontextmanager
    async def lifespan(application: FastAPI):
        try:
            artifact = artifact_loader(
                joblib_path=resolved_settings.model_path,
                meta_path=resolved_settings.metadata_path,
                validation_path=resolved_settings.validation_path,
            )
        except Exception as exc:
            logger.exception(
                "Classifier startup failed. Verify the three artifact configuration variables "
                "and the validation sidecar."
            )
            raise RuntimeError(
                "Validated complaint classifier failed to load; review the server log and "
                "artifact configuration."
            ) from exc

        application.state.model_artifact = artifact
        logger.info(
            "Validated complaint classifier loaded successfully (model_version=%s).",
            artifact["model_version"],
        )
        try:
            yield
        finally:
            application.state.model_artifact = None

    application = FastAPI(
        title="GovernanceIntelligence Internal Complaint Classifier",
        version="1.0",
        lifespan=lifespan,
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )
    application.state.model_artifact = None
    application.state.service_settings = resolved_settings

    @application.exception_handler(RequestValidationError)
    async def sanitized_validation_error(
        _request: Request, _exc: RequestValidationError
    ) -> JSONResponse:
        # Do not echo rejected complaint text or Pydantic's raw input field.
        return JSONResponse(status_code=422, content={"detail": "Invalid request payload."})

    @application.get("/health", response_model=HealthResponse)
    async def health() -> HealthResponse:
        artifact = application.state.model_artifact
        if artifact is None:
            raise HTTPException(status_code=503, detail="Classifier is not ready.")
        return HealthResponse(status="ready", model_version=artifact["model_version"])

    @application.post("/predict", response_model=PredictionResponse)
    async def predict(request: PredictionRequest) -> PredictionResponse:
        artifact = application.state.model_artifact
        if artifact is None:
            raise HTTPException(status_code=503, detail="Classifier is not ready.")
        if len(request.complaint_text) > resolved_settings.max_request_chars:
            raise HTTPException(
                status_code=413,
                detail=(
                    "complaint_text exceeds the configured operational limit of "
                    f"{resolved_settings.max_request_chars} characters."
                ),
            )

        try:
            result = predictor(request.complaint_text, artifact, request.case_id)
            return PredictionResponse.model_validate(result)
        except HTTPException:
            raise
        except Exception:
            # Complaint content and case identifiers are intentionally absent.
            logger.exception("Complaint prediction failed.")
            raise HTTPException(status_code=500, detail="Prediction failed.") from None

    return application


app = create_app()

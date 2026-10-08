"""Internal HTTP boundary for validated GovernanceIntelligence ML artifacts.

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
from pydantic import BaseModel, ConfigDict, Field, StrictInt, StrictStr, field_validator
from starlette.concurrency import run_in_threadpool

_SRC_DIR = Path(__file__).resolve().parent
_ML_DIR = _SRC_DIR.parent
sys.path.insert(0, str(_SRC_DIR))
sys.path.insert(0, str(_ML_DIR))

from predict_v1 import (
    DEFAULT_JOBLIB,
    DEFAULT_META,
    DEFAULT_VALIDATION,
    load_versioned_artifact,
    predict_complaint,
)
from anomaly_detection.bundle import (
    DEFAULT_BUNDLE_CONFIG,
    TrustedLocalBundle,
    load_trusted_local_bundle,
)
from anomaly_detection.inference import InferenceInputError, infer_payload_with_bundle

logger = logging.getLogger(__name__)

DEFAULT_MAX_REQUEST_CHARS = 10_000
DEFAULT_MAX_ANOMALY_REQUEST_BYTES = 262_144
DEFAULT_MAX_ANOMALY_EVENTS = 500
MODEL_PATH_ENV = "GOVERNANCE_MODEL_PATH"
METADATA_PATH_ENV = "GOVERNANCE_MODEL_METADATA_PATH"
VALIDATION_PATH_ENV = "GOVERNANCE_MODEL_VALIDATION_PATH"
MAX_REQUEST_CHARS_ENV = "GOVERNANCE_MAX_REQUEST_CHARS"
ANOMALY_BUNDLE_CONFIG_ENV = "GOVERNANCE_ANOMALY_BUNDLE_CONFIG"
MAX_ANOMALY_REQUEST_BYTES_ENV = "GOVERNANCE_ANOMALY_MAX_REQUEST_BYTES"
MAX_ANOMALY_EVENTS_ENV = "GOVERNANCE_ANOMALY_MAX_EVENTS"

ArtifactLoader = Callable[..., dict[str, Any]]
Predictor = Callable[[str, dict[str, Any], str | None], dict[str, Any]]
AnomalyBundleLoader = Callable[[str | Path], TrustedLocalBundle]
AnomalyPredictor = Callable[..., dict[str, Any]]


@dataclass(frozen=True)
class ServiceSettings:
    """Deployment configuration. Defaults are independent of the process CWD."""

    model_path: Path
    metadata_path: Path
    validation_path: Path
    max_request_chars: int = DEFAULT_MAX_REQUEST_CHARS
    anomaly_bundle_config: Path = DEFAULT_BUNDLE_CONFIG
    max_anomaly_request_bytes: int = DEFAULT_MAX_ANOMALY_REQUEST_BYTES
    max_anomaly_events: int = DEFAULT_MAX_ANOMALY_EVENTS

    @classmethod
    def from_environment(cls) -> "ServiceSettings":
        def configured_path(name: str, default: Path) -> Path:
            configured = os.getenv(name)
            return Path(configured).expanduser().resolve() if configured else default.resolve()

        def positive_limit(name: str, default: int) -> int:
            raw_value = os.getenv(name, str(default))
            try:
                value = int(raw_value)
            except ValueError as exc:
                raise ValueError(f"{name} must be a positive integer") from exc
            if value <= 0:
                raise ValueError(f"{name} must be a positive integer")
            return value

        return cls(
            model_path=configured_path(MODEL_PATH_ENV, DEFAULT_JOBLIB),
            metadata_path=configured_path(METADATA_PATH_ENV, DEFAULT_META),
            validation_path=configured_path(VALIDATION_PATH_ENV, DEFAULT_VALIDATION),
            max_request_chars=positive_limit(
                MAX_REQUEST_CHARS_ENV, DEFAULT_MAX_REQUEST_CHARS
            ),
            anomaly_bundle_config=configured_path(
                ANOMALY_BUNDLE_CONFIG_ENV, DEFAULT_BUNDLE_CONFIG
            ),
            max_anomaly_request_bytes=positive_limit(
                MAX_ANOMALY_REQUEST_BYTES_ENV, DEFAULT_MAX_ANOMALY_REQUEST_BYTES
            ),
            max_anomaly_events=positive_limit(
                MAX_ANOMALY_EVENTS_ENV, DEFAULT_MAX_ANOMALY_EVENTS
            ),
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


class WorkflowEventRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    event_seq: StrictInt = Field(gt=0)
    activity: StrictStr
    institution: StrictStr
    resource: StrictStr
    timestamp: StrictStr

    @field_validator("activity", "institution", "resource", "timestamp")
    @classmethod
    def event_text_must_not_be_blank(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("event text fields must not be blank")
        return value


class WorkflowAnomalyRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    case_id: StrictStr
    events: list[WorkflowEventRequest]

    @field_validator("case_id")
    @classmethod
    def case_id_must_not_be_blank(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("case_id must not be blank")
        return value


class WorkflowFeatureValues(BaseModel):
    model_config = ConfigDict(extra="forbid")

    event_count: float
    elapsed_days: float
    max_gap_hours: float
    mean_gap_hours: float
    unique_activities: float
    repeated_activity_count: float
    resource_handoffs: float
    institution_switches: float
    distinct_resources: float


class WorkflowAnomalyResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    case_id: str
    model_version: str
    anomaly_score: float
    threshold: float
    flagged: bool
    feature_values: WorkflowFeatureValues
    advisory_note: str


def create_app(
    settings: ServiceSettings | None = None,
    *,
    artifact_loader: ArtifactLoader = load_versioned_artifact,
    predictor: Predictor = predict_complaint,
    anomaly_bundle_loader: AnomalyBundleLoader = load_trusted_local_bundle,
    anomaly_predictor: AnomalyPredictor = infer_payload_with_bundle,
) -> FastAPI:
    """Create an app whose validated artifacts are loaded once at startup."""
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
            anomaly_bundle = anomaly_bundle_loader(resolved_settings.anomaly_bundle_config)
        except Exception as exc:
            logger.exception(
                "Workflow anomaly startup failed. Verify the trusted bundle configuration."
            )
            raise RuntimeError(
                "Validated workflow anomaly bundle failed to load; review the server log and "
                "artifact configuration."
            ) from exc

        application.state.anomaly_bundle = anomaly_bundle
        logger.info(
            "Validated workflow anomaly bundle loaded successfully (bundle_version=%s).",
            anomaly_bundle.bundle_version,
        )
        try:
            yield
        finally:
            application.state.model_artifact = None
            application.state.anomaly_bundle = None

    application = FastAPI(
        title="GovernanceIntelligence Internal ML Service",
        version="1.0",
        lifespan=lifespan,
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )
    application.state.model_artifact = None
    application.state.anomaly_bundle = None
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

    @application.post(
        "/workflow-anomaly/predict", response_model=WorkflowAnomalyResponse
    )
    async def predict_workflow_anomaly(
        request: WorkflowAnomalyRequest, http_request: Request
    ) -> WorkflowAnomalyResponse:
        bundle = application.state.anomaly_bundle
        if bundle is None:
            raise HTTPException(status_code=503, detail="Workflow anomaly model is not ready.")
        request_size = len(await http_request.body())
        if request_size > resolved_settings.max_anomaly_request_bytes:
            raise HTTPException(
                status_code=413,
                detail=(
                    "Request body exceeds the configured operational limit of "
                    f"{resolved_settings.max_anomaly_request_bytes} bytes."
                ),
            )
        if len(request.events) > resolved_settings.max_anomaly_events:
            raise HTTPException(
                status_code=413,
                detail=(
                    "events exceeds the configured operational limit of "
                    f"{resolved_settings.max_anomaly_events}."
                ),
            )

        try:
            result = await run_in_threadpool(
                anomaly_predictor,
                request.model_dump(mode="python"),
                bundle=bundle,
            )
            return WorkflowAnomalyResponse.model_validate(result)
        except InferenceInputError:
            raise HTTPException(
                status_code=422,
                detail="Invalid or unsupported complete workflow trace.",
            ) from None
        except HTTPException:
            raise
        except Exception:
            # Do not log raw trace content, case identifiers, or exception text.
            logger.error("Workflow anomaly prediction failed.")
            raise HTTPException(
                status_code=500, detail="Workflow anomaly prediction failed."
            ) from None

    return application


app = create_app()

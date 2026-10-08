"""Validated local CLI inference for complete workflow traces only."""

from __future__ import annotations

from datetime import datetime
import json
import math
from pathlib import Path
from typing import Any, Mapping

import numpy as np

from .baseline import anomaly_scores, flags_from_scores
from .bundle import DEFAULT_BUNDLE_CONFIG, TrustedLocalBundle, load_trusted_local_bundle
from .features import EventRecord, TraceValidationError, extract_features, validate_complete_trace


class InferenceInputError(ValueError):
    """Raised when a payload is not a complete, feature-compatible workflow trace."""


def _required_string(value: Any, *, field: str, event_index: int | None = None) -> str:
    location = f" event {event_index}" if event_index is not None else ""
    if not isinstance(value, str) or not value.strip():
        raise InferenceInputError(f"{field}{location} must be a nonblank string")
    return value


def trace_from_payload(payload: Mapping[str, Any]) -> tuple[str, list[EventRecord]]:
    """Parse a documented JSON payload without sorting or repairing any event."""

    if not isinstance(payload, Mapping):
        raise InferenceInputError("input must be a JSON object")
    forbidden_model_fields = {"model_path", "model", "bundle_path", "threshold"} & set(payload)
    if forbidden_model_fields:
        raise InferenceInputError(
            "model and threshold paths are trusted local configuration, not input payload fields"
        )
    case_id = _required_string(payload.get("case_id"), field="case_id")
    events_value = payload.get("events")
    if not isinstance(events_value, list) or not events_value:
        raise InferenceInputError("events must be a nonempty JSON array for a complete trace")

    events: list[EventRecord] = []
    required = {"event_seq", "activity", "institution", "resource", "timestamp"}
    for index, raw_event in enumerate(events_value, start=1):
        if not isinstance(raw_event, Mapping):
            raise InferenceInputError(f"event {index} must be a JSON object")
        missing = sorted(required - set(raw_event))
        if missing:
            raise InferenceInputError(f"event {index} is missing required fields: {', '.join(missing)}")
        sequence = raw_event["event_seq"]
        if isinstance(sequence, bool) or not isinstance(sequence, int) or sequence <= 0:
            raise InferenceInputError(f"event_seq event {index} must be a positive integer")
        activity = _required_string(raw_event["activity"], field="activity", event_index=index)
        institution = _required_string(raw_event["institution"], field="institution", event_index=index)
        resource = _required_string(raw_event["resource"], field="resource", event_index=index)
        timestamp_text = _required_string(raw_event["timestamp"], field="timestamp", event_index=index)
        try:
            timestamp = datetime.fromisoformat(timestamp_text)
        except ValueError as exc:
            raise InferenceInputError(f"timestamp event {index} is not ISO-8601 parseable") from exc
        if timestamp.tzinfo is not None:
            raise InferenceInputError(
                f"timestamp event {index} must remain naive because dataset timezone semantics are unspecified"
            )
        events.append(
            EventRecord(
                case_id=case_id,
                event_seq=sequence,
                activity=activity,
                institution=institution,
                resource=resource,
                timestamp_text=timestamp_text,
                timestamp=timestamp,
            )
        )
    try:
        validate_complete_trace(events)
    except TraceValidationError as exc:
        raise InferenceInputError(
            f"unsupported partial or invalid workflow trace: {exc}"
        ) from exc
    return case_id, events


def infer_payload(
    payload: Mapping[str, Any], *, bundle_config: str | Path = DEFAULT_BUNDLE_CONFIG
) -> dict[str, Any]:
    """Load the verified local bundle and score one complete trace."""

    case_id, events = trace_from_payload(payload)
    bundle = load_trusted_local_bundle(bundle_config)
    return _infer_validated_trace(case_id, events, bundle=bundle)


def infer_payload_with_bundle(
    payload: Mapping[str, Any], *, bundle: TrustedLocalBundle
) -> dict[str, Any]:
    """Score one complete trace with an already verified bundle.

    Service adapters use this entry point so artifact verification and model
    deserialization happen once during process startup, never per request.
    """

    case_id, events = trace_from_payload(payload)
    return _infer_validated_trace(case_id, events, bundle=bundle)


def _infer_validated_trace(
    case_id: str, events: list[EventRecord], *, bundle: TrustedLocalBundle
) -> dict[str, Any]:
    """Apply the frozen numerical boundary to one validated complete trace."""

    feature_values = extract_features(events)
    feature_order = bundle.manifest["feature_order"]
    if list(feature_values) != feature_order:
        raise InferenceInputError("extracted feature order does not match the verified bundle")
    matrix = np.asarray([[float(feature_values[feature]) for feature in feature_order]], dtype=float)
    if not np.isfinite(matrix).all():
        raise InferenceInputError("extracted feature values contain missing or non-finite values")
    score = float(anomaly_scores(bundle.frozen_run.model, matrix)[0])
    flagged = bool(flags_from_scores(np.asarray([score]), bundle.frozen_run.threshold)[0])
    return {
        "case_id": case_id,
        "model_version": bundle.bundle_version,
        "anomaly_score": score,
        "threshold": bundle.frozen_run.threshold,
        "flagged": flagged,
        "feature_values": feature_values,
        "advisory_note": (
            "Synthetic-reference advisory only: this anomaly score is not a probability, confidence, "
            "corruption finding, legal-compliance determination, or explanation of feature causation. "
            "It is valid only for complete traces under the supplied synthetic workflow contract."
        ),
    }


def infer_json_file(
    input_path: str | Path, *, bundle_config: str | Path = DEFAULT_BUNDLE_CONFIG
) -> dict[str, Any]:
    path = Path(input_path)
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise InferenceInputError(f"cannot read inference JSON input '{path}': {exc}") from exc
    return infer_payload(payload, bundle_config=bundle_config)

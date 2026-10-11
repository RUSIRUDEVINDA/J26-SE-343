"""Fictional unit fixtures for the workflow-anomaly data boundary."""

from __future__ import annotations

from datetime import datetime

import pytest

from anomaly_detection.features import (
    EventRecord,
    TraceValidationError,
    canonicalize_activity,
    extract_features,
    validate_complete_trace,
)
from anomaly_detection.validation import (
    DatasetValidationError,
    validate_model_input_columns,
    validate_split_isolation,
)


def event(
    sequence: int,
    activity: str,
    timestamp: str,
    *,
    institution: str = "District",
    resource: str = "Officer-1",
) -> EventRecord:
    """Create one small fictional event without using dataset content."""

    return EventRecord(
        case_id="FICTIONAL-CASE-1",
        event_seq=sequence,
        activity=activity,
        institution=institution,
        resource=resource,
        timestamp_text=timestamp,
        timestamp=datetime.fromisoformat(timestamp),
    )


def completed_trace() -> list[EventRecord]:
    return [
        event(1, "Fictional Intake", "2030-01-01T08:00:00"),
        event(2, "Fictional Review", "2030-01-01T10:00:00"),
        event(3, "DS Final Notification", "2030-01-02T10:00:00"),
    ]


def test_validate_complete_trace_when_timestamp_moves_backwards_should_reject() -> None:
    trace = completed_trace()
    trace[1] = event(2, "Fictional Review", "2030-01-01T07:00:00")

    with pytest.raises(TraceValidationError, match="backward timestamp"):
        validate_complete_trace(trace)


def test_validate_complete_trace_when_final_notification_is_not_last_should_reject() -> None:
    trace = [
        event(1, "Fictional Intake", "2030-01-01T08:00:00"),
        event(2, "DS Final Notification", "2030-01-01T10:00:00"),
        event(3, "Fictional Follow-up", "2030-01-01T11:00:00"),
    ]

    with pytest.raises(TraceValidationError, match="not last"):
        validate_complete_trace(trace)


def test_extract_features_when_completed_trace_should_use_exact_nine_definitions() -> None:
    features = extract_features(completed_trace())

    assert features == {
        "event_count": 3,
        "elapsed_days": 26 / 24,
        "max_gap_hours": 24.0,
        "mean_gap_hours": 13.0,
        "unique_activities": 3,
        "repeated_activity_count": 0,
        "resource_handoffs": 0,
        "institution_switches": 0,
        "distinct_resources": 1,
    }


def test_extract_features_when_resource_name_is_shared_should_scope_identity_by_institution() -> None:
    trace = [
        event(1, "Fictional Intake", "2030-01-01T08:00:00", institution="District", resource="Shared-7"),
        event(2, "Fictional Review", "2030-01-01T09:00:00", institution="Survey", resource="Shared-7"),
        event(3, "DS Final Notification", "2030-01-01T10:00:00", institution="Survey", resource="Shared-7"),
    ]

    features = extract_features(trace)

    assert features["resource_handoffs"] == 1
    assert features["institution_switches"] == 1
    assert features["distinct_resources"] == 2


def test_canonicalize_activity_when_documented_terminal_suffix_should_preserve_raw_and_only_strip_suffix() -> None:
    canonical, marker = canonicalize_activity("Fictional Review (Correction Requested)")
    untouched, no_marker = canonicalize_activity("Fictional Review (Repeated) later")

    assert canonical == "Fictional Review"
    assert marker == "Correction Requested"
    assert untouched == "Fictional Review (Repeated) later"
    assert no_marker == ""


def test_validate_split_isolation_when_exact_trace_group_crosses_splits_should_reject() -> None:
    manifest = [
        {"case_id": "FICTIONAL-A", "split": "train", "exact_trace_group": "same-trace"},
        {"case_id": "FICTIONAL-B", "split": "test", "exact_trace_group": "same-trace"},
    ]

    with pytest.raises(DatasetValidationError, match="exact trace group overlap"):
        validate_split_isolation(manifest)


def test_validate_model_input_columns_when_label_is_present_should_reject_leakage() -> None:
    leaked_columns = [
        "event_count",
        "elapsed_days",
        "max_gap_hours",
        "mean_gap_hours",
        "unique_activities",
        "repeated_activity_count",
        "resource_handoffs",
        "institution_switches",
        "distinct_resources",
        "label",
    ]

    with pytest.raises(DatasetValidationError, match="leaked excluded columns"):
        validate_model_input_columns(leaked_columns, ["case_id", "label"])

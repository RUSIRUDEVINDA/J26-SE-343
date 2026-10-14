"""Exact feature definitions for completed workflow traces."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
import re
from statistics import mean
from typing import Sequence


FEATURE_NAMES = (
    "event_count",
    "elapsed_days",
    "max_gap_hours",
    "mean_gap_hours",
    "unique_activities",
    "repeated_activity_count",
    "resource_handoffs",
    "institution_switches",
    "distinct_resources",
)

FINAL_ACTIVITY = "DS Final Notification"
_SUFFIX_PATTERN = re.compile(
    r" \((?P<marker>Repeated|Re-submission|Correction Requested)\)$"
)


class TraceValidationError(ValueError):
    """Raised when a trace is outside the supported completed-trace contract."""


@dataclass(frozen=True, slots=True)
class EventRecord:
    """A validated event with its raw activity preserved."""

    case_id: str
    event_seq: int
    activity: str
    institution: str
    resource: str
    timestamp_text: str
    timestamp: datetime


def canonicalize_activity(raw_activity: str) -> tuple[str, str]:
    """Return canonical activity and generator marker using only documented suffixes."""

    match = _SUFFIX_PATTERN.search(raw_activity)
    if match is None:
        return raw_activity, ""
    return _SUFFIX_PATTERN.sub("", raw_activity), match.group("marker")


def validate_complete_trace(events: Sequence[EventRecord]) -> None:
    """Reject invalid ordering, backward time, or unsupported incomplete traces.

    Input order is authoritative event-sequence order.  This function never
    sorts events and never repairs timestamps.
    """

    if len(events) < 2:
        raise TraceValidationError("complete traces require at least two events")

    case_ids = {event.case_id for event in events}
    if len(case_ids) != 1:
        raise TraceValidationError("a trace must contain exactly one case_id")

    actual_sequence = [event.event_seq for event in events]
    expected_sequence = list(range(1, len(events) + 1))
    if actual_sequence != expected_sequence:
        raise TraceValidationError(
            "event_seq must be contiguous from 1 in supplied row order; "
            f"received {actual_sequence}"
        )

    for previous, current in zip(events, events[1:]):
        if current.timestamp < previous.timestamp:
            raise TraceValidationError(
                "backward timestamp in event_seq order: "
                f"event {current.event_seq} precedes event {previous.event_seq}"
            )

    final_positions = [
        index for index, event in enumerate(events) if event.activity == FINAL_ACTIVITY
    ]
    if len(final_positions) != 1:
        raise TraceValidationError(
            f"complete traces require exactly one raw '{FINAL_ACTIVITY}' event"
        )
    if final_positions[0] != len(events) - 1:
        raise TraceValidationError(
            f"unsupported incomplete trace: raw '{FINAL_ACTIVITY}' is not last"
        )


def extract_features(events: Sequence[EventRecord]) -> dict[str, int | float]:
    """Compute the exact nine-feature contract from event-sequence order."""

    validate_complete_trace(events)

    timestamps = [event.timestamp for event in events]
    gaps_hours = [
        (current - previous).total_seconds() / 3600.0
        for previous, current in zip(timestamps, timestamps[1:])
    ]
    canonical_activities = [canonicalize_activity(event.activity)[0] for event in events]
    resource_identities = [
        (event.institution, event.resource) for event in events
    ]
    unique_activity_count = len(set(canonical_activities))

    return {
        "event_count": len(events),
        "elapsed_days": (timestamps[-1] - timestamps[0]).total_seconds() / 86400.0,
        "max_gap_hours": max(gaps_hours),
        "mean_gap_hours": mean(gaps_hours),
        "unique_activities": unique_activity_count,
        "repeated_activity_count": len(events) - unique_activity_count,
        "resource_handoffs": sum(
            previous != current
            for previous, current in zip(resource_identities, resource_identities[1:])
        ),
        "institution_switches": sum(
            previous.institution != current.institution
            for previous, current in zip(events, events[1:])
        ),
        "distinct_resources": len(set(resource_identities)),
    }

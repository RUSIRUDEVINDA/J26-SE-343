"""Strict, non-repairing CSV and JSON loaders for workflow-anomaly data."""

from __future__ import annotations

import csv
from datetime import datetime
import json
import math
from pathlib import Path
from typing import Iterable, Mapping, Sequence

from .features import EventRecord, FEATURE_NAMES, canonicalize_activity


class DatasetFormatError(ValueError):
    """Raised when a dataset file violates its structural contract."""


def load_json(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise DatasetFormatError(f"cannot read JSON file '{path}': {exc}") from exc
    if not isinstance(value, dict):
        raise DatasetFormatError(f"JSON root must be an object: '{path}'")
    return value


def load_csv(
    path: Path,
    required_columns: Iterable[str],
    *,
    exact_columns: Sequence[str] | None = None,
) -> tuple[list[str], list[dict[str, str]]]:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            columns = list(reader.fieldnames or [])
            if len(columns) != len(set(columns)):
                raise DatasetFormatError(f"duplicate CSV columns in '{path}'")
            missing = sorted(set(required_columns) - set(columns))
            if missing:
                raise DatasetFormatError(
                    f"missing required columns in '{path}': {', '.join(missing)}"
                )
            if exact_columns is not None and columns != list(exact_columns):
                raise DatasetFormatError(
                    f"unexpected columns or order in '{path}': expected "
                    f"{list(exact_columns)}, received {columns}"
                )
            rows = list(reader)
    except (OSError, UnicodeError, csv.Error) as exc:
        raise DatasetFormatError(f"cannot read CSV file '{path}': {exc}") from exc

    for row_number, row in enumerate(rows, start=2):
        if None in row:
            raise DatasetFormatError(f"extra CSV fields in '{path}' row {row_number}")
    return columns, rows


def require_nonblank(
    row: Mapping[str, str], columns: Iterable[str], path: Path, row_number: int
) -> None:
    blank = [column for column in columns if not str(row.get(column, "")).strip()]
    if blank:
        raise DatasetFormatError(
            f"blank required values in '{path}' row {row_number}: {', '.join(blank)}"
        )


def require_unique_ids(
    rows: Sequence[Mapping[str, str]], path: Path, *, column: str = "case_id"
) -> None:
    seen: set[str] = set()
    for row_number, row in enumerate(rows, start=2):
        value = str(row.get(column, "")).strip()
        if not value:
            raise DatasetFormatError(
                f"blank {column} in '{path}' row {row_number}"
            )
        if value in seen:
            raise DatasetFormatError(
                f"duplicate {column} '{value}' in '{path}' row {row_number}"
            )
        seen.add(value)


def parse_finite_number(value: str, *, path: Path, row_number: int, column: str) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError) as exc:
        raise DatasetFormatError(
            f"non-numeric {column} in '{path}' row {row_number}: {value!r}"
        ) from exc
    if not math.isfinite(number):
        raise DatasetFormatError(
            f"non-finite {column} in '{path}' row {row_number}: {value!r}"
        )
    return number


def parse_naive_timestamp(value: str, *, path: Path, row_number: int) -> datetime:
    try:
        timestamp = datetime.fromisoformat(value)
    except (TypeError, ValueError) as exc:
        raise DatasetFormatError(
            f"invalid timestamp in '{path}' row {row_number}: {value!r}"
        ) from exc
    if timestamp.tzinfo is not None:
        raise DatasetFormatError(
            f"timezone-aware timestamp is outside the unspecified-naive contract in "
            f"'{path}' row {row_number}"
        )
    return timestamp


def load_events(
    path: Path,
    *,
    prepared: bool,
) -> tuple[list[dict[str, str]], dict[str, list[EventRecord]]]:
    base_columns = ("case_id", "event_seq", "activity", "institution", "resource", "timestamp")
    required = base_columns + (
        ("activity_canonical", "simulation_marker", "record_origin") if prepared else ()
    )
    _, rows = load_csv(path, required)

    event_keys: set[tuple[str, int]] = set()
    grouped: dict[str, list[EventRecord]] = {}
    for row_number, row in enumerate(rows, start=2):
        require_nonblank(row, base_columns, path, row_number)
        try:
            event_seq = int(row["event_seq"])
        except ValueError as exc:
            raise DatasetFormatError(
                f"event_seq must be an integer in '{path}' row {row_number}"
            ) from exc
        if event_seq <= 0 or str(event_seq) != row["event_seq"].strip():
            raise DatasetFormatError(
                f"event_seq must be a positive canonical integer in '{path}' row {row_number}"
            )
        key = (row["case_id"], event_seq)
        if key in event_keys:
            raise DatasetFormatError(f"duplicate event key {key!r} in '{path}'")
        event_keys.add(key)

        timestamp = parse_naive_timestamp(
            row["timestamp"], path=path, row_number=row_number
        )
        event = EventRecord(
            case_id=row["case_id"],
            event_seq=event_seq,
            activity=row["activity"],
            institution=row["institution"],
            resource=row["resource"],
            timestamp_text=row["timestamp"],
            timestamp=timestamp,
        )
        grouped.setdefault(event.case_id, []).append(event)

        if prepared:
            canonical, marker = canonicalize_activity(event.activity)
            if row["activity_canonical"] != canonical:
                raise DatasetFormatError(
                    f"activity_canonical mismatch in '{path}' row {row_number}"
                )
            if row["simulation_marker"] != marker:
                raise DatasetFormatError(
                    f"simulation_marker mismatch in '{path}' row {row_number}"
                )

    return rows, grouped


def load_feature_rows(
    path: Path, *, include_case_id: bool
) -> tuple[list[dict[str, str]], dict[str, dict[str, float]] | None]:
    expected = (("case_id",) if include_case_id else ()) + FEATURE_NAMES
    _, rows = load_csv(path, expected, exact_columns=expected)
    if include_case_id:
        require_unique_ids(rows, path)

    by_id: dict[str, dict[str, float]] = {}
    for row_number, row in enumerate(rows, start=2):
        values = {
            feature: parse_finite_number(
                row[feature], path=path, row_number=row_number, column=feature
            )
            for feature in FEATURE_NAMES
        }
        if any(value < 0 for value in values.values()):
            raise DatasetFormatError(
                f"negative feature value in '{path}' row {row_number}"
            )
        if values["event_count"] != values["unique_activities"] + values["repeated_activity_count"]:
            raise DatasetFormatError(
                f"event/activity-count invariant failed in '{path}' row {row_number}"
            )
        if include_case_id:
            by_id[row["case_id"]] = values
    return rows, by_id if include_case_id else None

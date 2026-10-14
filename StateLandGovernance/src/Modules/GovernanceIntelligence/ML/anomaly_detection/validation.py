"""End-to-end validation and feature reconciliation for synthetic workflow v1."""

from __future__ import annotations

from collections import Counter
import hashlib
import json
import math
from pathlib import Path
from typing import Iterable, Mapping, Sequence

from .features import (
    FEATURE_NAMES,
    EventRecord,
    TraceValidationError,
    extract_features,
    validate_complete_trace,
)
from .loader import (
    DatasetFormatError,
    load_csv,
    load_events,
    load_feature_rows,
    load_json,
    parse_finite_number,
    require_nonblank,
    require_unique_ids,
)


# Prepared features are serialized as decimal text from binary floats.  This
# tolerance covers representation round-trips without accepting 2-decimal
# source-feature rounding or materially different values.
SERIALIZATION_ABS_TOLERANCE = 1e-9
SERIALIZATION_REL_TOLERANCE = 1e-12
SUPPORTED_SPLITS = ("train", "validation", "test")


class DatasetValidationError(ValueError):
    """Raised when any integrity, isolation, or reconciliation check fails."""


def _fail(message: str) -> None:
    raise DatasetValidationError(message)


def _same_number(actual: int | float, expected: float) -> bool:
    return math.isclose(
        float(actual),
        float(expected),
        rel_tol=SERIALIZATION_REL_TOLERANCE,
        abs_tol=SERIALIZATION_ABS_TOLERANCE,
    )


def verify_checksums(dataset_dir: Path) -> dict[str, int]:
    checksum_path = dataset_dir / "checksums.json"
    checksums = load_json(checksum_path)
    if not checksums:
        _fail("checksums.json contains no file entries")

    root = dataset_dir.resolve()
    matched = 0
    for relative_name, expected_hash in checksums.items():
        if not isinstance(relative_name, str) or not isinstance(expected_hash, str):
            _fail("checksums.json keys and values must be strings")
        target = (dataset_dir / Path(relative_name)).resolve()
        if not target.is_relative_to(root) or target == checksum_path.resolve():
            _fail(f"unsafe checksum path: {relative_name!r}")
        if not target.is_file():
            _fail(f"checksummed file is missing: {relative_name}")
        actual_hash = hashlib.sha256(target.read_bytes()).hexdigest()
        if actual_hash.lower() != expected_hash.lower():
            _fail(
                f"checksum mismatch for {relative_name}: "
                f"expected={expected_hash.lower()} actual={actual_hash.lower()}"
            )
        matched += 1
    return {"listed": len(checksums), "matched": matched, "failed": 0}


def _validate_contract(dataset_dir: Path) -> dict:
    contract = load_json(dataset_dir / "feature_contract.json")
    features = contract.get("model_features")
    if features != list(FEATURE_NAMES):
        _fail(
            "feature_contract.json does not contain the exact supported nine-feature order"
        )
    timezone = str(contract.get("timezone", ""))
    if "Unspecified naive timestamps" not in timezone or "no UTC assignment" not in timezone:
        _fail("feature contract no longer preserves unspecified naive timezone semantics")
    scoring_scope = str(contract.get("scoring_scope", ""))
    if "complete accepted synthetic case only" not in scoring_scope:
        _fail("feature contract no longer limits scoring to complete accepted traces")
    return contract


def _trace_fingerprint(events: Sequence[EventRecord]) -> str:
    first = events[0].timestamp
    behaviour = [
        [
            event.activity,
            event.institution,
            event.resource,
            round((event.timestamp - first).total_seconds(), 6),
        ]
        for event in events
    ]
    payload = json.dumps(behaviour, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def _raw_trace_rejection(events: Sequence[EventRecord]) -> set[str]:
    reasons: set[str] = set()
    sequence = [event.event_seq for event in events]
    if sequence != list(range(1, len(events) + 1)):
        reasons.add("INVALID_EVENT_SEQUENCE")
    if any(current.timestamp < previous.timestamp for previous, current in zip(events, events[1:])):
        reasons.add("NON_MONOTONIC_EVENT_TIME")
    final_positions = [
        index
        for index, event in enumerate(events)
        if event.activity == "DS Final Notification"
    ]
    if len(final_positions) != 1:
        reasons.add("UNSUPPORTED_COMPLETION_MARKER")
    else:
        final_index = final_positions[0]
        final_time = events[final_index].timestamp
        if final_index != len(events) - 1:
            reasons.add("FINAL_NOTIFICATION_NOT_LAST")
        if any(event.timestamp > final_time for event in events):
            reasons.add("EVENT_AFTER_FINAL_NOTIFICATION")
    return reasons


def _validate_originals(
    dataset_dir: Path,
) -> tuple[list[dict[str, str]], list[dict[str, str]], dict[str, list[EventRecord]], set[str]]:
    case_path = dataset_dir / "originals" / "case_features.csv"
    required_case_columns = (
        "case_id",
        "label",
        "perturbation_type",
        "land_extent_perches",
    ) + FEATURE_NAMES
    _, cases = load_csv(case_path, required_case_columns)
    require_unique_ids(cases, case_path)
    for row_number, row in enumerate(cases, start=2):
        require_nonblank(row, required_case_columns, case_path, row_number)
        for column in ("land_extent_perches",) + FEATURE_NAMES:
            parse_finite_number(
                row[column], path=case_path, row_number=row_number, column=column
            )

    event_path = dataset_dir / "originals" / "event_log.csv"
    event_rows, event_groups = load_events(event_path, prepared=False)
    case_ids = {row["case_id"] for row in cases}
    if set(event_groups) != case_ids:
        _fail("original event case IDs do not match original case-feature IDs")

    rejected_ids: set[str] = set()
    for case_id, events in event_groups.items():
        if _raw_trace_rejection(events):
            rejected_ids.add(case_id)
    return cases, event_rows, event_groups, rejected_ids


def _validate_catalog_and_manifest(
    dataset_dir: Path,
    original_case_ids: set[str],
    original_events: Mapping[str, Sequence[EventRecord]],
) -> tuple[
    list[dict[str, str]],
    list[dict[str, str]],
    set[str],
    set[str],
]:
    catalog_path = dataset_dir / "prepared" / "case_catalog.csv"
    catalog_required = (
        "case_id",
        "label",
        "perturbation_type",
        "source_parent_case_id",
        "source_family_id",
        "lineage_status",
        "exact_trace_group",
        "preparation_status",
        "exclusion_reasons",
    )
    _, catalog = load_csv(catalog_path, catalog_required)
    require_unique_ids(catalog, catalog_path)
    if {row["case_id"] for row in catalog} != original_case_ids:
        _fail("case_catalog case IDs do not match the original case population")

    accepted_ids: set[str] = set()
    quarantined_ids: set[str] = set()
    catalog_by_id: dict[str, dict[str, str]] = {}
    for row_number, row in enumerate(catalog, start=2):
        require_nonblank(
            row,
            ("case_id", "label", "perturbation_type", "lineage_status", "exact_trace_group", "preparation_status"),
            catalog_path,
            row_number,
        )
        case_id = row["case_id"]
        catalog_by_id[case_id] = row
        if row["source_parent_case_id"] or row["source_family_id"]:
            _fail(f"missing generator lineage was inferred for case {case_id}")
        if row["lineage_status"] != "not_supplied":
            _fail(f"missing lineage is not explicitly 'not_supplied' for case {case_id}")
        actual_fingerprint = _trace_fingerprint(original_events[case_id])
        if row["exact_trace_group"] != actual_fingerprint:
            _fail(f"exact_trace_group mismatch for case {case_id}")
        if row["preparation_status"] == "ACCEPTED_SYNTHETIC":
            accepted_ids.add(case_id)
            if row["exclusion_reasons"]:
                _fail(f"accepted case {case_id} has exclusion reasons")
        elif row["preparation_status"] == "QUARANTINED":
            quarantined_ids.add(case_id)
            if not row["exclusion_reasons"]:
                _fail(f"quarantined case {case_id} has no exclusion reason")
        else:
            _fail(f"unsupported preparation_status for case {case_id}")

    manifest_path = dataset_dir / "splits" / "split_manifest.csv"
    manifest_columns = (
        "case_id",
        "exact_trace_group",
        "label",
        "perturbation_type",
        "split",
        "model_fit_eligible",
    )
    _, manifest = load_csv(
        manifest_path, manifest_columns, exact_columns=manifest_columns
    )
    require_unique_ids(manifest, manifest_path)
    if {row["case_id"] for row in manifest} != original_case_ids:
        _fail("split_manifest case IDs do not match the original case population")

    for row in manifest:
        catalog_row = catalog_by_id[row["case_id"]]
        for column in ("exact_trace_group", "label", "perturbation_type"):
            if row[column] != catalog_row[column]:
                _fail(
                    f"split_manifest {column} disagrees with case_catalog for "
                    f"case {row['case_id']}"
                )
        if row["split"] not in (*SUPPORTED_SPLITS, "quarantine"):
            _fail(f"unsupported split value for case {row['case_id']}")
        expected_eligible = row["split"] == "train"
        if row["model_fit_eligible"] not in ("True", "False"):
            _fail(f"invalid model_fit_eligible for case {row['case_id']}")
        if (row["model_fit_eligible"] == "True") != expected_eligible:
            _fail(f"model_fit_eligible disagrees with split for case {row['case_id']}")
        if row["split"] == "train" and not (
            row["label"] == "reference-normal" and row["perturbation_type"] == "none"
        ):
            _fail("training contains a non-reference or perturbed case")

    validate_split_isolation(manifest)
    manifest_quarantine = {row["case_id"] for row in manifest if row["split"] == "quarantine"}
    if manifest_quarantine != quarantined_ids:
        _fail("manifest quarantine IDs do not match case_catalog quarantine IDs")
    return catalog, manifest, accepted_ids, quarantined_ids


def validate_split_isolation(manifest: Sequence[Mapping[str, str]]) -> None:
    """Validate ID and exact-trace-group isolation across model-facing splits."""

    ids = {
        split: {row["case_id"] for row in manifest if row["split"] == split}
        for split in SUPPORTED_SPLITS
    }
    groups = {
        split: {
            row["exact_trace_group"] for row in manifest if row["split"] == split
        }
        for split in SUPPORTED_SPLITS
    }
    for index, left in enumerate(SUPPORTED_SPLITS):
        for right in SUPPORTED_SPLITS[index + 1 :]:
            id_overlap = ids[left] & ids[right]
            if id_overlap:
                _fail(f"case ID overlap between {left} and {right}: {sorted(id_overlap)}")
            group_overlap = groups[left] & groups[right]
            if group_overlap:
                _fail(
                    f"exact trace group overlap between {left} and {right}: "
                    f"{sorted(group_overlap)}"
                )


def validate_model_input_columns(columns: Sequence[str], excluded_inputs: Iterable[str]) -> None:
    """Require the exact feature-only model matrix schema and reject leakage."""

    if list(columns) != list(FEATURE_NAMES):
        leaked = sorted(set(columns) & set(excluded_inputs))
        suffix = f"; leaked excluded columns: {leaked}" if leaked else ""
        _fail(
            f"model matrix must contain exactly the nine contract features in order{suffix}"
        )


def validate_row_alignment(
    split: str,
    keys: Sequence[Mapping[str, str]],
    x_rows: Sequence[Mapping[str, str]],
    feature_by_id: Mapping[str, Mapping[str, float]],
    labels: Sequence[Mapping[str, str]] | None,
) -> None:
    """Validate X/key/label row identity without moving IDs into model inputs."""

    if len(keys) != len(x_rows):
        _fail(f"X_{split} and keys_{split} row counts differ")
    if labels is not None and len(labels) != len(keys):
        _fail(f"y_{split} and keys_{split} row counts differ")
    for index, (key_row, x_row) in enumerate(zip(keys, x_rows), start=1):
        case_id = key_row["case_id"]
        if case_id not in feature_by_id:
            _fail(f"keys_{split} row {index} references unknown prepared case {case_id}")
        expected = feature_by_id[case_id]
        for feature in FEATURE_NAMES:
            actual = float(x_row[feature])
            if not _same_number(actual, expected[feature]):
                _fail(
                    f"X_{split} row {index} feature {feature} does not align with "
                    f"key {case_id}"
                )
        if labels is not None and labels[index - 1]["case_id"] != case_id:
            _fail(f"y_{split} row {index} does not align with key {case_id}")


def _validate_prepared_and_reconcile(
    dataset_dir: Path,
    contract: Mapping[str, object],
    accepted_ids: set[str],
    original_events: Mapping[str, Sequence[EventRecord]],
) -> tuple[int, int, dict[str, dict[str, float]], list[dict[str, str]], dict[str, list[EventRecord]]]:
    event_path = dataset_dir / "prepared" / "event_log.csv"
    prepared_rows, prepared_events = load_events(event_path, prepared=True)
    if set(prepared_events) != accepted_ids:
        _fail("prepared event case IDs do not match accepted case IDs")

    for case_id, events in prepared_events.items():
        try:
            validate_complete_trace(events)
        except TraceValidationError as exc:
            _fail(f"prepared case {case_id} is invalid: {exc}")

        original_by_key = {
            event.event_seq: event for event in original_events[case_id]
        }
        for event in events:
            original = original_by_key.get(event.event_seq)
            if original is None or (
                event.activity,
                event.institution,
                event.resource,
                event.timestamp_text,
            ) != (
                original.activity,
                original.institution,
                original.resource,
                original.timestamp_text,
            ):
                _fail(
                    f"prepared raw event fields differ from originals for "
                    f"{case_id}/{event.event_seq}"
                )

    feature_path = dataset_dir / "prepared" / "case_features.csv"
    _, feature_by_id = load_feature_rows(feature_path, include_case_id=True)
    assert feature_by_id is not None
    if set(feature_by_id) != accepted_ids:
        _fail("prepared feature case IDs do not match accepted case IDs")

    compared_values = 0
    for case_id, expected in feature_by_id.items():
        actual = extract_features(prepared_events[case_id])
        for feature in FEATURE_NAMES:
            compared_values += 1
            if not _same_number(actual[feature], expected[feature]):
                _fail(
                    f"feature reconciliation mismatch for {case_id}/{feature}: "
                    f"recomputed={actual[feature]!r} prepared={expected[feature]!r}"
                )

    excluded_inputs = contract.get("excluded_inputs")
    if not isinstance(excluded_inputs, list) or not all(
        isinstance(value, str) for value in excluded_inputs
    ):
        _fail("feature contract excluded_inputs must be a string list")
    return len(prepared_rows), compared_values, feature_by_id, prepared_rows, prepared_events


def _validate_quarantine(
    dataset_dir: Path,
    quarantined_ids: set[str],
    rejected_original_ids: set[str],
    original_events: Mapping[str, Sequence[EventRecord]],
) -> tuple[int, int]:
    if quarantined_ids != rejected_original_ids:
        missing = sorted(rejected_original_ids - quarantined_ids)
        extra = sorted(quarantined_ids - rejected_original_ids)
        _fail(
            "quarantine does not exactly contain rejected original traces: "
            f"missing={missing} extra={extra}"
        )

    cases_path = dataset_dir / "audit" / "quarantined_cases.csv"
    _, cases = load_csv(cases_path, ("case_id", "preparation_status", "exclusion_reasons"))
    require_unique_ids(cases, cases_path)
    if {row["case_id"] for row in cases} != quarantined_ids:
        _fail("quarantined_cases IDs do not match catalog quarantine IDs")

    events_path = dataset_dir / "audit" / "quarantined_events.csv"
    event_rows, event_groups = load_events(events_path, prepared=False)
    if set(event_groups) != quarantined_ids:
        _fail("quarantined_events IDs do not match catalog quarantine IDs")
    for case_id, events in event_groups.items():
        originals = original_events[case_id]
        if [
            (event.event_seq, event.activity, event.institution, event.resource, event.timestamp_text)
            for event in events
        ] != [
            (event.event_seq, event.activity, event.institution, event.resource, event.timestamp_text)
            for event in originals
        ]:
            _fail(f"quarantined raw events differ from originals for case {case_id}")
    return len(cases), len(event_rows)


def _validate_splits(
    dataset_dir: Path,
    contract: Mapping[str, object],
    manifest: Sequence[Mapping[str, str]],
    feature_by_id: Mapping[str, Mapping[str, float]],
    quarantined_ids: set[str],
) -> dict[str, dict[str, int]]:
    excluded_inputs = contract["excluded_inputs"]
    manifest_by_split = {
        split: [row for row in manifest if row["split"] == split]
        for split in SUPPORTED_SPLITS
    }
    split_report: dict[str, dict[str, int]] = {}
    model_facing_ids: set[str] = set()

    for split in SUPPORTED_SPLITS:
        key_path = dataset_dir / "splits" / f"keys_{split}.csv"
        _, keys = load_csv(key_path, ("case_id",), exact_columns=("case_id",))
        require_unique_ids(keys, key_path)
        key_ids = [row["case_id"] for row in keys]
        expected_ids = {row["case_id"] for row in manifest_by_split[split]}
        if set(key_ids) != expected_ids:
            _fail(f"keys_{split} IDs do not match split_manifest")
        model_facing_ids.update(key_ids)

        x_path = dataset_dir / "splits" / f"X_{split}.csv"
        columns, x_rows = load_csv(x_path, FEATURE_NAMES, exact_columns=FEATURE_NAMES)
        validate_model_input_columns(columns, excluded_inputs)
        for row_number, row in enumerate(x_rows, start=2):
            for feature in FEATURE_NAMES:
                parse_finite_number(
                    row[feature], path=x_path, row_number=row_number, column=feature
                )

        labels: list[dict[str, str]] | None = None
        if split != "train":
            y_path = dataset_dir / "splits" / f"y_{split}.csv"
            y_columns = ("case_id", "synthetic_anomaly_label", "perturbation_type")
            _, labels = load_csv(y_path, y_columns, exact_columns=y_columns)
            require_unique_ids(labels, y_path)
            manifest_by_id = {
                row["case_id"]: row for row in manifest_by_split[split]
            }
            for row_number, label_row in enumerate(labels, start=2):
                if label_row["synthetic_anomaly_label"] not in ("0", "1"):
                    _fail(f"invalid synthetic anomaly label in '{y_path}' row {row_number}")
                manifest_row = manifest_by_id.get(label_row["case_id"])
                if manifest_row is None:
                    _fail(f"y_{split} contains a case outside its manifest split")
                expected_label = (
                    "1" if manifest_row["label"] == "controlled-perturbation" else "0"
                )
                if label_row["synthetic_anomaly_label"] != expected_label:
                    _fail(f"y_{split} label disagrees with manifest")
                if label_row["perturbation_type"] != manifest_row["perturbation_type"]:
                    _fail(f"y_{split} perturbation type disagrees with manifest")
        elif (dataset_dir / "splits" / "y_train.csv").exists():
            _fail("y_train.csv must not exist; training is unsupervised and feature-only")

        validate_row_alignment(split, keys, x_rows, feature_by_id, labels)
        label_counts = Counter(row["label"] for row in manifest_by_split[split])
        split_report[split] = {
            "rows": len(keys),
            "reference": label_counts["reference-normal"],
            "controlled_perturbations": label_counts["controlled-perturbation"],
        }

    if model_facing_ids & quarantined_ids:
        _fail("quarantined cases entered a model-facing split")
    return split_report


def _assert_summary_count(summary: Mapping[str, object], key: str, actual: int) -> None:
    expected = summary.get(key)
    if isinstance(expected, bool) or not isinstance(expected, int):
        _fail(f"audit summary field {key} must be an integer")
    if actual != expected:
        _fail(f"audit summary count mismatch for {key}: expected={expected} actual={actual}")


def validate_dataset(dataset_dir: str | Path) -> dict[str, object]:
    """Validate the extracted dataset and reconcile all prepared feature values.

    The source ZIP is deliberately not accessed.  Its hash is reported only as
    metadata recorded by the supplied audit summary.
    """

    root = Path(dataset_dir)
    if not root.is_dir():
        raise DatasetValidationError(f"dataset directory does not exist: {root}")

    try:
        checksum_report = verify_checksums(root)
        contract = _validate_contract(root)
        summary = load_json(root / "audit" / "audit_summary.json")
        original_cases, original_event_rows, original_events, rejected_ids = _validate_originals(root)
        original_case_ids = {row["case_id"] for row in original_cases}
        catalog, manifest, accepted_ids, quarantined_ids = _validate_catalog_and_manifest(
            root, original_case_ids, original_events
        )
        (
            accepted_event_count,
            reconciled_value_count,
            feature_by_id,
            _,
            _,
        ) = _validate_prepared_and_reconcile(
            root, contract, accepted_ids, original_events
        )
        quarantined_case_count, quarantined_event_count = _validate_quarantine(
            root, quarantined_ids, rejected_ids, original_events
        )
        split_report = _validate_splits(
            root, contract, manifest, feature_by_id, quarantined_ids
        )
    except (DatasetFormatError, OSError) as exc:
        raise DatasetValidationError(str(exc)) from exc

    actual_counts = {
        "source_cases": len(original_cases),
        "source_events": len(original_event_rows),
        "accepted_cases": len(accepted_ids),
        "accepted_events": accepted_event_count,
        "quarantined_cases": quarantined_case_count,
        "quarantined_events": quarantined_event_count,
    }
    for key, actual in actual_counts.items():
        _assert_summary_count(summary, key, actual)

    if summary.get("model_fit_performed") is not False:
        _fail("audit summary unexpectedly claims a model fit was performed")

    recorded_zip_hash = summary.get("source_zip_sha256")
    return {
        "status": "PASS",
        "dataset": str(root.resolve()),
        "checksum_verification": checksum_report,
        "source_zip_verification": {
            "status": "NOT_PERFORMED_SOURCE_ZIP_UNAVAILABLE",
            "recorded_sha256": recorded_zip_hash,
        },
        "counts": actual_counts,
        "splits": split_report,
        "feature_reconciliation": {
            "cases": len(feature_by_id),
            "features_per_case": len(FEATURE_NAMES),
            "values_compared": reconciled_value_count,
            "mismatches": 0,
            "absolute_tolerance": SERIALIZATION_ABS_TOLERANCE,
            "relative_tolerance": SERIALIZATION_REL_TOLERANCE,
        },
        "isolation": {
            "case_id_overlap": 0,
            "exact_trace_group_overlap": 0,
            "quarantined_cases_in_model_splits": 0,
        },
        "lineage": {
            "status": "UNKNOWN_NOT_SUPPLIED",
            "cases_with_claimed_parent_or_family": 0,
        },
        "model_fit_performed": False,
        "interpretation": {
            "reference_normal": "generator-designated reference; not independently verified normality",
            "routing_config": "preserved simulation configuration; not treated as Sri Lankan law",
        },
    }

"""Trusted local artifact verification and versioned bundle support.

Only locally configured artifacts below this package's directory are accepted.
Model locations are never read from inference payloads.
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
import math
import os
from pathlib import Path
from typing import Any, Mapping

import joblib
from sklearn.ensemble import IsolationForest

from .baseline import (
    BASELINE_NAME,
    METADATA_FILENAME,
    MODEL_FILENAME,
    MODEL_HASH_FILENAME,
    MODEL_PARAMETERS,
    SCORE_DIRECTION,
    THRESHOLD_FILENAME,
    BaselineError,
)
from .features import FEATURE_NAMES
from .loader import load_json
from .validation import validate_dataset


ANOMALY_ROOT = Path(__file__).resolve().parent
BUNDLE_VERSION = "workflow_anomaly_model_v1"
DEFAULT_DATASET_DIRECTORY = ANOMALY_ROOT / "data" / "synthetic_workflow_v1"
DEFAULT_RUN_DIRECTORY = ANOMALY_ROOT / "runs" / BASELINE_NAME
DEFAULT_EVALUATION_DIRECTORY = ANOMALY_ROOT / "evaluations" / "synthetic_held_out_test_v1"
DEFAULT_BUNDLE_DIRECTORY = ANOMALY_ROOT / "bundles" / BUNDLE_VERSION
DEFAULT_BUNDLE_CONFIG = DEFAULT_BUNDLE_DIRECTORY / "local_inference_config.json"


class ArtifactVerificationError(BaselineError):
    """Raised when trusted local model evidence is absent or inconsistent."""


@dataclass(frozen=True, slots=True)
class VerifiedFrozenRun:
    dataset_directory: Path
    run_directory: Path
    model_path: Path
    model: IsolationForest
    metadata: dict[str, Any]
    threshold_record: dict[str, Any]
    threshold: float


@dataclass(frozen=True, slots=True)
class TrustedLocalBundle:
    bundle_directory: Path
    bundle_version: str
    manifest: dict[str, Any]
    frozen_run: VerifiedFrozenRun


def sha256_file(path: Path) -> str:
    try:
        return hashlib.sha256(path.read_bytes()).hexdigest()
    except OSError as exc:
        raise ArtifactVerificationError(f"required artifact is unavailable: {path}") from exc


def _require_object(value: Any, *, description: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ArtifactVerificationError(f"{description} must be a JSON object")
    return value


def _require_exact_feature_order(value: Any, *, description: str) -> None:
    if value != list(FEATURE_NAMES):
        raise ArtifactVerificationError(
            f"{description} does not match the frozen nine-feature order"
        )


def _resolve_trusted_path(base_directory: Path, relative_path: str, *, description: str) -> Path:
    if not isinstance(relative_path, str) or not relative_path:
        raise ArtifactVerificationError(f"{description} path is missing")
    target = (base_directory / relative_path).resolve()
    root = ANOMALY_ROOT.resolve()
    if not target.is_relative_to(root):
        raise ArtifactVerificationError(f"{description} path leaves the trusted local anomaly root")
    if not target.is_file():
        raise ArtifactVerificationError(f"{description} artifact is missing: {target}")
    return target


def _verify_hash_entry(entry: Mapping[str, Any], *, base_directory: Path, description: str) -> Path:
    path = _resolve_trusted_path(base_directory, entry.get("path"), description=description)
    expected_hash = entry.get("sha256")
    if not isinstance(expected_hash, str) or len(expected_hash) != 64:
        raise ArtifactVerificationError(f"{description} SHA-256 evidence is missing or malformed")
    actual_hash = sha256_file(path)
    if actual_hash != expected_hash:
        raise ArtifactVerificationError(
            f"{description} SHA-256 mismatch: expected={expected_hash} actual={actual_hash}"
        )
    return path


def verify_frozen_run(
    dataset_directory: str | Path = DEFAULT_DATASET_DIRECTORY,
    run_directory: str | Path = DEFAULT_RUN_DIRECTORY,
) -> VerifiedFrozenRun:
    """Verify milestone-2 evidence before any held-out scoring or inference."""

    dataset = Path(dataset_directory).resolve()
    run = Path(run_directory).resolve()
    try:
        report = validate_dataset(dataset)
    except Exception as exc:  # Preserve one clear artifact boundary error for callers.
        raise ArtifactVerificationError(f"dataset boundary validation failed: {exc}") from exc
    if report.get("status") != "PASS":
        raise ArtifactVerificationError("dataset boundary validation did not pass")

    required_paths = {
        "model": run / MODEL_FILENAME,
        "metadata": run / METADATA_FILENAME,
        "threshold": run / THRESHOLD_FILENAME,
        "model_hash": run / MODEL_HASH_FILENAME,
    }
    for description, path in required_paths.items():
        if not path.is_file():
            raise ArtifactVerificationError(f"missing frozen-run {description} artifact: {path}")

    metadata = _require_object(load_json(required_paths["metadata"]), description="metadata")
    frozen = _require_object(load_json(required_paths["threshold"]), description="threshold record")
    contract = _require_object(load_json(dataset / "feature_contract.json"), description="feature contract")

    if metadata.get("baseline_name") != BASELINE_NAME or frozen.get("baseline_name") != BASELINE_NAME:
        raise ArtifactVerificationError("baseline name does not match the frozen milestone-2 model")
    _require_exact_feature_order(metadata.get("feature_order"), description="metadata feature order")
    _require_exact_feature_order(frozen.get("feature_order"), description="threshold feature order")
    _require_exact_feature_order(contract.get("model_features"), description="dataset feature contract")
    if frozen.get("feature_contract") != contract:
        raise ArtifactVerificationError("saved feature contract differs from the validated dataset contract")
    contract_hash = sha256_file(dataset / "feature_contract.json")
    if frozen.get("feature_contract_sha256") != contract_hash:
        raise ArtifactVerificationError("saved feature-contract hash does not match the dataset")

    if metadata.get("score_direction") != SCORE_DIRECTION or frozen.get("score_direction") != SCORE_DIRECTION:
        raise ArtifactVerificationError("saved score direction is not the frozen anomaly-score direction")
    policy = _require_object(frozen.get("threshold_policy"), description="threshold policy")
    if policy != metadata.get("threshold_policy"):
        raise ArtifactVerificationError("threshold policy differs between metadata and saved threshold")
    if policy.get("quantile") != 0.95 or policy.get("quantile_method") != "linear":
        raise ArtifactVerificationError("threshold policy differs from frozen validation quantile settings")
    if policy.get("reference_case_count") != 80 or policy.get("strict_comparison") != "anomaly_score > threshold":
        raise ArtifactVerificationError("threshold policy does not preserve reference-only strict comparison")
    try:
        threshold = float(frozen["threshold"])
    except (KeyError, TypeError, ValueError) as exc:
        raise ArtifactVerificationError("saved threshold is missing or invalid") from exc
    if not math.isfinite(threshold):
        raise ArtifactVerificationError("saved threshold is non-finite")

    expected_input_files = {
        "feature_contract": dataset / "feature_contract.json",
        "dataset_readme": dataset / "README.md",
        "audit_summary": dataset / "audit" / "audit_summary.json",
        "split_manifest": dataset / "splits" / "split_manifest.csv",
        "X_train": dataset / "splits" / "X_train.csv",
        "keys_train": dataset / "splits" / "keys_train.csv",
        "X_validation": dataset / "splits" / "X_validation.csv",
        "keys_validation": dataset / "splits" / "keys_validation.csv",
        "y_validation": dataset / "splits" / "y_validation.csv",
        "case_catalog": dataset / "prepared" / "case_catalog.csv",
    }
    recorded_hashes = _require_object(metadata.get("input_sha256"), description="input hashes")
    for name, path in expected_input_files.items():
        if recorded_hashes.get(name) != sha256_file(path):
            raise ArtifactVerificationError(f"frozen input hash mismatch for {name}")
    split_hash = sha256_file(expected_input_files["split_manifest"])
    if metadata.get("split_manifest_sha256") != split_hash:
        raise ArtifactVerificationError("frozen split-manifest hash mismatch")
    if metadata.get("training", {}).get("rows") != 240 or metadata.get("training", {}).get("source") != "splits/X_train.csv only":
        raise ArtifactVerificationError("metadata does not prove the frozen 240-row training reference")
    if metadata.get("training", {}).get("labels_or_ids_passed_to_fit") is not False:
        raise ArtifactVerificationError("metadata does not preserve feature-only training")
    if metadata.get("preprocessing") != "none; raw validated numerical feature matrix used directly":
        raise ArtifactVerificationError("unexpected learned preprocessing in frozen run")

    model_hash = sha256_file(required_paths["model"])
    if metadata.get("model_sha256") != model_hash:
        raise ArtifactVerificationError("model hash does not match metadata")
    hash_line = required_paths["model_hash"].read_text(encoding="utf-8").strip().split()
    if hash_line != [model_hash, MODEL_FILENAME]:
        raise ArtifactVerificationError("model.sha256 does not match the saved model artifact")

    # The hash was checked before this local, saved-model deserialization.
    model = joblib.load(required_paths["model"])
    if not isinstance(model, IsolationForest):
        raise ArtifactVerificationError("saved model is not an IsolationForest")
    parameters = model.get_params(deep=False)
    for name, expected in MODEL_PARAMETERS.items():
        if parameters.get(name) != expected:
            raise ArtifactVerificationError(
                f"saved IsolationForest parameter mismatch for {name}: "
                f"expected={expected!r} actual={parameters.get(name)!r}"
            )
    if metadata.get("actual_estimator_parameters") != parameters:
        raise ArtifactVerificationError("metadata estimator parameters do not match the saved model")
    if getattr(model, "n_features_in_", None) != len(FEATURE_NAMES):
        raise ArtifactVerificationError("saved model feature count does not match the contract")

    return VerifiedFrozenRun(
        dataset_directory=dataset,
        run_directory=run,
        model_path=required_paths["model"],
        model=model,
        metadata=metadata,
        threshold_record=frozen,
        threshold=threshold,
    )


def create_versioned_bundle(
    frozen_run: VerifiedFrozenRun,
    evaluation_directory: str | Path,
    bundle_directory: str | Path = DEFAULT_BUNDLE_DIRECTORY,
) -> Path:
    """Create the non-overwriting, trusted-local inference bundle manifest."""

    evaluation = Path(evaluation_directory).resolve()
    bundle = Path(bundle_directory)
    if bundle.exists():
        raise FileExistsError(f"Refusing to overwrite existing model bundle: {bundle}")
    required_evidence = {
        "test_metrics": evaluation / "test_metrics.json",
        "test_predictions": evaluation / "test_predictions.csv",
        "evaluation_metadata": evaluation / "evaluation_metadata.json",
    }
    for description, path in required_evidence.items():
        if not path.is_file():
            raise ArtifactVerificationError(f"missing held-out evaluation evidence: {description}")

    bundle.mkdir(parents=True, exist_ok=False)
    try:
        def entry(path: Path) -> dict[str, str]:
            return {
                "path": os.path.relpath(path, bundle).replace("\\", "/"),
                "sha256": sha256_file(path),
            }

        artifacts = {
            "model": entry(frozen_run.model_path),
            "metadata": entry(frozen_run.run_directory / METADATA_FILENAME),
            "threshold_and_feature_contract": entry(frozen_run.run_directory / THRESHOLD_FILENAME),
            **{name: entry(path) for name, path in required_evidence.items()},
        }
        manifest: dict[str, Any] = {
            "bundle_version": BUNDLE_VERSION,
            "trusted_local_only": True,
            "feature_order": list(FEATURE_NAMES),
            "score_direction": SCORE_DIRECTION,
            "threshold": frozen_run.threshold,
            "strict_flag_rule": "anomaly_score > threshold",
            "artifacts": artifacts,
            "limitations": [
                "Synthetic-only completed-case workflow experiment.",
                "Not a corruption detector, legal compliance engine, or validated Sri Lankan departmental model.",
                "Anomaly score is not a probability or confidence value.",
                "No individual-feature attribution is supplied by this bundle.",
            ],
        }
        config: dict[str, Any] = {
            "bundle_version": BUNDLE_VERSION,
            "bundle_manifest": "bundle_manifest.json",
            "trusted_local_artifact_paths": {
                name: item["path"] for name, item in artifacts.items()
            },
            "payload_model_paths_allowed": False,
        }
        (bundle / "bundle_manifest.json").write_text(
            json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        (bundle / "local_inference_config.json").write_text(
            json.dumps(config, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
    except Exception:
        # Do not remove the new directory: non-overwrite semantics retain failure evidence.
        raise
    return bundle.resolve()


def load_trusted_local_bundle(config_path: str | Path = DEFAULT_BUNDLE_CONFIG) -> TrustedLocalBundle:
    """Load and cross-check a bundle from trusted local configuration only."""

    config_file = Path(config_path).resolve()
    root = ANOMALY_ROOT.resolve()
    if not config_file.is_relative_to(root):
        raise ArtifactVerificationError("local bundle configuration is outside the trusted anomaly root")
    if not config_file.is_file():
        raise ArtifactVerificationError(f"local bundle configuration is missing: {config_file}")
    config = _require_object(load_json(config_file), description="local inference configuration")
    if config.get("bundle_version") != BUNDLE_VERSION or config.get("payload_model_paths_allowed") is not False:
        raise ArtifactVerificationError("local inference configuration is not the trusted bundle version")
    manifest_path = _resolve_trusted_path(
        config_file.parent, config.get("bundle_manifest"), description="bundle manifest"
    )
    manifest = _require_object(load_json(manifest_path), description="bundle manifest")
    if manifest.get("bundle_version") != BUNDLE_VERSION or manifest.get("trusted_local_only") is not True:
        raise ArtifactVerificationError("bundle manifest is not marked as trusted local versioned evidence")
    _require_exact_feature_order(manifest.get("feature_order"), description="bundle feature order")
    if manifest.get("score_direction") != SCORE_DIRECTION or manifest.get("strict_flag_rule") != "anomaly_score > threshold":
        raise ArtifactVerificationError("bundle does not preserve the frozen score/threshold policy")
    if not math.isfinite(float(manifest.get("threshold", float("nan")))):
        raise ArtifactVerificationError("bundle threshold is missing or non-finite")

    artifacts = _require_object(manifest.get("artifacts"), description="bundle artifacts")
    configured_paths = _require_object(
        config.get("trusted_local_artifact_paths"), description="configured artifact paths"
    )
    required = (
        "model",
        "metadata",
        "threshold_and_feature_contract",
        "test_metrics",
        "test_predictions",
        "evaluation_metadata",
    )
    resolved: dict[str, Path] = {}
    for name in required:
        item = _require_object(artifacts.get(name), description=f"bundle artifact {name}")
        if configured_paths.get(name) != item.get("path"):
            raise ArtifactVerificationError(f"configured path does not match manifest evidence for {name}")
        resolved[name] = _verify_hash_entry(item, base_directory=config_file.parent, description=name)

    frozen = verify_frozen_run(
        dataset_directory=DEFAULT_DATASET_DIRECTORY,
        run_directory=resolved["model"].parent,
    )
    if resolved["metadata"] != frozen.run_directory / METADATA_FILENAME or resolved["threshold_and_feature_contract"] != frozen.run_directory / THRESHOLD_FILENAME:
        raise ArtifactVerificationError("bundle metadata or threshold path does not belong to the verified model run")
    if float(manifest["threshold"]) != frozen.threshold:
        raise ArtifactVerificationError("bundle threshold differs from verified frozen threshold")
    evaluation_metadata = _require_object(
        load_json(resolved["evaluation_metadata"]), description="held-out evaluation metadata"
    )
    if evaluation_metadata.get("metric_label") != "synthetic held-out evaluation":
        raise ArtifactVerificationError("held-out evaluation evidence is not labelled synthetic")
    if evaluation_metadata.get("model_sha256") != frozen.metadata["model_sha256"]:
        raise ArtifactVerificationError("held-out evaluation evidence links a different model")
    if evaluation_metadata.get("feature_order") != list(FEATURE_NAMES):
        raise ArtifactVerificationError("held-out evaluation evidence has a feature-order mismatch")
    if float(evaluation_metadata.get("threshold", float("nan"))) != frozen.threshold:
        raise ArtifactVerificationError("held-out evaluation evidence has a threshold mismatch")
    if evaluation_metadata.get("score_direction") != SCORE_DIRECTION or evaluation_metadata.get("strict_flag_rule") != "anomaly_score > threshold":
        raise ArtifactVerificationError("held-out evaluation evidence has a score-policy mismatch")
    if evaluation_metadata.get("fit_performed") is not False or evaluation_metadata.get("test_scored_once") is not True:
        raise ArtifactVerificationError("held-out evaluation evidence does not preserve evaluation-only semantics")
    expected_test_inputs = {
        "X_test": DEFAULT_DATASET_DIRECTORY / "splits" / "X_test.csv",
        "keys_test": DEFAULT_DATASET_DIRECTORY / "splits" / "keys_test.csv",
        "y_test": DEFAULT_DATASET_DIRECTORY / "splits" / "y_test.csv",
        "split_manifest": DEFAULT_DATASET_DIRECTORY / "splits" / "split_manifest.csv",
    }
    evaluation_hashes = _require_object(
        evaluation_metadata.get("input_sha256"), description="held-out evaluation input hashes"
    )
    for name, path in expected_test_inputs.items():
        if evaluation_hashes.get(name) != sha256_file(path):
            raise ArtifactVerificationError(f"held-out evaluation input hash mismatch for {name}")
    metrics = _require_object(load_json(resolved["test_metrics"]), description="held-out test metrics")
    if metrics.get("metric_label") != "synthetic held-out evaluation":
        raise ArtifactVerificationError("held-out test metrics lack the required synthetic label")
    return TrustedLocalBundle(
        bundle_directory=config_file.parent,
        bundle_version=BUNDLE_VERSION,
        manifest=manifest,
        frozen_run=frozen,
    )

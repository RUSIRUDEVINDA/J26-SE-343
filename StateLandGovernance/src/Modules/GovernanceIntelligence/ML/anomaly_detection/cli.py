"""CLI for workflow-anomaly dataset validation and feature reconciliation."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

from .baseline import BASELINE_NAME, BaselineError, run_frozen_baseline, verify_saved_validation_scores
from .bundle import (
    DEFAULT_BUNDLE_CONFIG,
    DEFAULT_BUNDLE_DIRECTORY,
    DEFAULT_EVALUATION_DIRECTORY,
    ArtifactVerificationError,
    load_trusted_local_bundle,
)
from .evaluation import evaluate_reserved_test_once
from .inference import InferenceInputError, infer_json_file
from .validation import DatasetValidationError, validate_dataset


DEFAULT_DATASET = Path(__file__).resolve().parent / "data" / "synthetic_workflow_v1"
DEFAULT_RUN_DIRECTORY = Path(__file__).resolve().parent / "runs" / BASELINE_NAME


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Validate extracted workflow-anomaly data and reconcile the exact "
            "nine-feature contract without fitting a model."
        )
    )
    parser.add_argument(
        "command",
        choices=(
            "validate",
            "reconcile",
            "train-baseline",
            "verify-baseline",
            "evaluate-test",
            "verify-bundle",
            "infer",
        ),
        help="Validate data, operate the frozen baseline, evaluate its reserved test once, or run trusted-local inference.",
    )
    parser.add_argument(
        "--dataset",
        type=Path,
        default=DEFAULT_DATASET,
        help=f"Extracted dataset directory (default: {DEFAULT_DATASET})",
    )
    parser.add_argument(
        "--run-directory",
        type=Path,
        default=DEFAULT_RUN_DIRECTORY,
        help=f"New baseline output directory (default: {DEFAULT_RUN_DIRECTORY})",
    )
    parser.add_argument(
        "--evaluation-directory",
        type=Path,
        default=DEFAULT_EVALUATION_DIRECTORY,
        help=f"Non-overwriting held-out evaluation output (default: {DEFAULT_EVALUATION_DIRECTORY})",
    )
    parser.add_argument(
        "--bundle-directory",
        type=Path,
        default=DEFAULT_BUNDLE_DIRECTORY,
        help=f"Non-overwriting trusted local bundle directory (default: {DEFAULT_BUNDLE_DIRECTORY})",
    )
    parser.add_argument(
        "--bundle-config",
        type=Path,
        default=DEFAULT_BUNDLE_CONFIG,
        help=f"Trusted local bundle configuration (default: {DEFAULT_BUNDLE_CONFIG})",
    )
    parser.add_argument(
        "--input-json",
        type=Path,
        help="Complete workflow-trace JSON for the infer command; model paths are not accepted in payloads.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        if args.command == "train-baseline":
            report = run_frozen_baseline(args.dataset, args.run_directory)
        elif args.command == "verify-baseline":
            report = verify_saved_validation_scores(args.run_directory)
        elif args.command == "evaluate-test":
            report = evaluate_reserved_test_once(
                args.dataset,
                args.run_directory,
                args.evaluation_directory,
                args.bundle_directory,
            )
        elif args.command == "verify-bundle":
            bundle = load_trusted_local_bundle(args.bundle_config)
            report = {
                "status": "PASS",
                "bundle_version": bundle.bundle_version,
                "bundle_directory": str(bundle.bundle_directory),
                "model_sha256": bundle.frozen_run.metadata["model_sha256"],
                "threshold": bundle.frozen_run.threshold,
            }
        elif args.command == "infer":
            if args.input_json is None:
                parser.error("infer requires --input-json")
            report = infer_json_file(args.input_json, bundle_config=args.bundle_config)
        else:
            report = validate_dataset(args.dataset)
    except (DatasetValidationError, BaselineError, ArtifactVerificationError, InferenceInputError, FileExistsError) as exc:
        print(
            json.dumps(
                {"status": "FAIL", "dataset": str(args.dataset), "error": str(exc)},
                indent=2,
            ),
            file=sys.stderr,
        )
        return 1

    if args.command == "reconcile":
        output = {
            "status": report["status"],
            "dataset": report["dataset"],
            "checksum_verification": report["checksum_verification"],
            "source_zip_verification": report["source_zip_verification"],
            "feature_reconciliation": report["feature_reconciliation"],
        }
    else:
        output = report
    print(json.dumps(output, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

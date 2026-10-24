"""Validate a packaged complaint classifier in a clean Python process.

This utility performs inference-contract checks only. It does not train, alter,
or serialize the estimator, and it never emits the fictional complaint text.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

from config import LABEL_ORDER
from predict_v1 import load_versioned_artifact, predict_complaint


FICTIONAL_SMOKE_INPUTS = (
    "A fictional registry clerk altered the approval date after the review meeting.",
    "A fictional lessee has not paid the annual lease rent despite formal notices.",
    "A fictional parcel was transferred to another occupant without written authority.",
    "A fictional protected wetland lease was used to dump construction waste.",
)
PROBABILITY_TOLERANCE = 1e-10


def _sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def validate_fresh_process(
    joblib_path: Path,
    metadata_path: Path,
    validation_path: Path,
    expected_version: str = "v2",
) -> dict:
    artifact = load_versioned_artifact(joblib_path, metadata_path, validation_path)
    if artifact["model_version"] != expected_version:
        raise ValueError(
            f"Expected model version {expected_version!r}, got {artifact['model_version']!r}"
        )

    cases = []
    for index, complaint in enumerate(FICTIONAL_SMOKE_INPUTS, start=1):
        result = predict_complaint(complaint, artifact, case_id=f"FICTIONAL-SMOKE-{index}")
        probabilities = result["class_probabilities"]
        if set(probabilities) != set(LABEL_ORDER) or len(probabilities) != 4:
            raise ValueError("Smoke result does not contain the fixed four probability keys")
        values = [float(probabilities[label]) for label in LABEL_ORDER]
        if not all(math.isfinite(value) and 0.0 <= value <= 1.0 for value in values):
            raise ValueError("Smoke probabilities must be finite and inside [0, 1]")
        probability_sum = sum(values)
        if not math.isclose(probability_sum, 1.0, rel_tol=0.0, abs_tol=PROBABILITY_TOLERANCE):
            raise ValueError("Smoke probabilities do not sum to one")
        predicted = result["predicted_category"]
        if predicted not in LABEL_ORDER:
            raise ValueError("Smoke prediction is outside the fixed taxonomy")
        argmax = max(LABEL_ORDER, key=lambda label: probabilities[label])
        if predicted != argmax:
            raise ValueError("Smoke prediction does not equal probability argmax")
        cases.append(
            {
                "case_id": f"FICTIONAL-SMOKE-{index}",
                "input_sha256": _sha256_text(complaint),
                "predicted_category": predicted,
                "probability_keys": list(probabilities),
                "probability_sum": probability_sum,
                "contract_status": "PASS",
            }
        )

    return {
        "status": "PASS",
        "process_mode": "fresh_python_process",
        "model_version": artifact["model_version"],
        "fictional_input_count": len(cases),
        "probability_tolerance": PROBABILITY_TOLERANCE,
        "complaint_text_recorded": False,
        "predetermined_class_assertions": False,
        "training_invoked": False,
        "cases": cases,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--joblib", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--validation", type=Path, required=True)
    parser.add_argument("--expected-version", default="v2")
    args = parser.parse_args()
    result = validate_fresh_process(
        args.joblib, args.metadata, args.validation, args.expected_version
    )
    print(json.dumps(result, separators=(",", ":"), ensure_ascii=False))


if __name__ == "__main__":
    main()

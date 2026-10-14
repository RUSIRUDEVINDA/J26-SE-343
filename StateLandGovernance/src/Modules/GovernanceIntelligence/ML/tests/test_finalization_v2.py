"""Focused regression tests for v1 validation and corrected report logic."""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import pandas as pd
import pytest

_ML_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_ML_ROOT / "src"))

from boilerplate_transformer import build_pipeline_with_cleaner
from config import LABEL_ORDER
from finalize_experiment4_report import compute_per_class, validate_oof
from train_v1 import verify_pipeline_against_arm_a


def _serialisable_params(pipeline) -> dict:
    def normalise(value):
        if isinstance(value, tuple):
            return [normalise(item) for item in value]
        if isinstance(value, list):
            return [normalise(item) for item in value]
        if value is None or isinstance(value, (bool, int, float, str)):
            return value
        return repr(value)

    return {
        key: normalise(value)
        for key, value in pipeline.get_params(deep=True).items()
    }


def _write_run_metadata(directory: Path, pipeline, **overrides) -> None:
    params = _serialisable_params(pipeline)
    params.update(overrides)
    metadata = {
        "pipeline": {
            "steps": [name for name, _ in pipeline.steps],
            "params_from_get_params": params,
        }
    }
    directory.mkdir()
    (directory / "run_metadata.json").write_text(json.dumps(metadata), encoding="utf-8")


class TestTrainingReferenceConfiguration:
    def test_reference_config_mismatch_is_rejected(self, tmp_path):
        pipeline = build_pipeline_with_cleaner()
        run_dir = tmp_path / "run"
        _write_run_metadata(run_dir, pipeline, clf__C=999.0)
        with pytest.raises(ValueError, match="clf__C"):
            verify_pipeline_against_arm_a(run_dir, pipeline)

    def test_missing_required_reference_is_rejected(self, tmp_path):
        pipeline = build_pipeline_with_cleaner()
        with pytest.raises(ValueError, match="run_metadata.json"):
            verify_pipeline_against_arm_a(tmp_path / "missing", pipeline)


class TestCorrectedReportValidation:
    def _valid_oof(self) -> pd.DataFrame:
        return pd.DataFrame(
            {
                "Research_ID": ["R1", "R2"],
                "True_Label": LABEL_ORDER[:2],
                "Predicted_Label": LABEL_ORDER[:2],
                "Fold": [1, 2],
            }
        )

    def test_duplicate_oof_ids_are_rejected(self):
        frame = self._valid_oof()
        frame["Research_ID"] = ["R1", "R1"]
        with pytest.raises(ValueError, match="duplicate Research_ID"):
            validate_oof(frame, "fictional OOF", expected_rows=2)

    def test_blank_oof_ids_are_rejected(self):
        frame = self._valid_oof()
        frame.loc[1, "Research_ID"] = "   "
        with pytest.raises(ValueError, match="blank Research_ID"):
            validate_oof(frame, "fictional OOF", expected_rows=2)

    def test_zero_tp_f1_is_zero_when_denominator_positive(self):
        rows = compute_per_class(
            [LABEL_ORDER[0], LABEL_ORDER[1]],
            [LABEL_ORDER[1], LABEL_ORDER[0]],
            labels=[LABEL_ORDER[0]],
        )
        assert rows[0]["TP"] == 0
        assert rows[0]["FP"] == 1
        assert rows[0]["FN"] == 1
        assert rows[0]["F1"] == 0.0
        assert math.isfinite(rows[0]["F1"])

    def test_absent_class_f1_is_explicitly_undefined(self):
        rows = compute_per_class(
            [LABEL_ORDER[0]],
            [LABEL_ORDER[0]],
            labels=[LABEL_ORDER[1]],
        )
        assert rows[0]["F1"] is None

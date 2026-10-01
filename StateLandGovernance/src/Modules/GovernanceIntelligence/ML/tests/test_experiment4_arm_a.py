"""
test_experiment4_arm_a.py
--------------------------
Tests for the Experiment 4 Arm A evaluator.

Covers:
  - Missing required files fail clearly (FileNotFoundError)
  - Missing required columns fail clearly (ValueError)
  - Synthetic and source-derived records cannot enter Arm A
  - Baseline ID-set validation (>=1 unique nonblank IDs; production enforces 200)
  - True_Label mandatory in Experiment 2 fold reference
  - Blank Combined_Group rejection before multi-fold check
  - Frozen fold and group-separation assertions are preserved
  - Probability columns mapped correctly by class name (meaningful controlled stub)
  - Probability validation: finite, [0,1], row sums, missing column → clear failure
  - compute_per_class_metrics: FPR, undefined-denominator, reason preserved
  - _safe_macro undefined propagation
  - run_reproduction_check: all 5 conditions gate PASS status
  - Nonempty explicit output_dir rejected before writing
  - Production 200-row population count enforcement

Tests do NOT:
  - Assert specific accuracy or F1 thresholds.
  - Assert specific confusion matrix values.
  - Fabricate benchmark results.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any
from unittest.mock import MagicMock, patch

import numpy as np
import pandas as pd
import pytest

# Add src/ to path
_SRC_DIR = Path(__file__).resolve().parent.parent / "src"
sys.path.insert(0, str(_SRC_DIR))

from config import LABEL_ORDER, ID_COLUMN, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN
import evaluate_experiment4 as ev4
from evaluate_experiment4 import (
    EXPECTED_PROB_COLS,
    PROB_TOLERANCE,
    PROB_ROW_SUM_TOLERANCE,
    _prob_col,
    _require_file,
    _require_columns,
    _validate_prob_matrix,
    _safe_macro,
    _dir_is_occupied,
    compute_per_class_metrics,
    load_and_filter_baseline,
    run_reproduction_check,
    validate_fold_reference,
    run_arm_a,
    run_experiment4_arm_a,
    BASELINE_ORIGIN,
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_baseline_df(n: int = 8) -> pd.DataFrame:
    """
    Create a minimal baseline DataFrame with n rows (n must be divisible by 4).
    Each of the four classes gets n//4 rows.
    """
    assert n % 4 == 0
    per_class = n // 4
    rows = []
    for idx, lbl in enumerate(LABEL_ORDER):
        for v in range(per_class):
            rid = f"BL-{idx:02d}{v:02d}"
            rows.append({
                ID_COLUMN: rid,
                TEXT_COLUMN: f"Complaint about {lbl.lower()} issue variant {v}.",
                TARGET_COLUMN: lbl,
                GROUP_COLUMN: f"GRP-{idx:02d}{v:02d}",
                "Record_Origin": BASELINE_ORIGIN,
            })
    return pd.DataFrame(rows)


def _make_exp2_oof(baseline_df: pd.DataFrame, tmp_path: Path) -> Path:
    """Create a minimal Experiment 2 OOF aligned to baseline_df with True_Label."""
    rids  = baseline_df[ID_COLUMN].tolist()
    n     = len(rids)
    folds = [(i % 5) + 1 for i in range(n)]
    rows  = [
        {
            ID_COLUMN: rid,
            "Combined_Group": f"CG-{rid}",
            "Fold": folds[i],
            "True_Label": baseline_df.iloc[i][TARGET_COLUMN],
            "Predicted_Label": baseline_df.iloc[i][TARGET_COLUMN],
        }
        for i, rid in enumerate(rids)
    ]
    p = tmp_path / "exp2_oof.csv"
    pd.DataFrame(rows).to_csv(p, index=False)
    return p


def _make_candidate_csv(baseline_df: pd.DataFrame, tmp_path: Path) -> Path:
    """Create a candidate CSV with baseline + 2 non-baseline rows."""
    extras = [
        {
            ID_COLUMN: "SYN-0001", TEXT_COLUMN: "Synthetic text.",
            TARGET_COLUMN: LABEL_ORDER[0], GROUP_COLUMN: "GRP-SYN",
            "Record_Origin": "New synthetic scenario",
        },
        {
            ID_COLUMN: "SRC-001", TEXT_COLUMN: "Source-derived text.",
            TARGET_COLUMN: LABEL_ORDER[1], GROUP_COLUMN: "GRP-SRC",
            "Record_Origin": "New source-derived candidate",
        },
    ]
    df = pd.concat([baseline_df, pd.DataFrame(extras)], ignore_index=True)
    p = tmp_path / "candidate.csv"
    df.to_csv(p, index=False)
    return p


def _make_repro_ref_csv(
    baseline_df: pd.DataFrame,
    exp2_oof_path: Path,
    tmp_path: Path,
    *,
    override_fold: dict | None = None,
    override_true_label: dict | None = None,
    override_pred_label: dict | None = None,
    drop_prob_col: str | None = None,
    nan_prob_col: str | None = None,
    inf_prob_col: str | None = None,
) -> Path:
    """
    Create a minimal reproduction reference CSV that looks like Exp3 Arm B OOF.
    All prob columns present and valid by default.
    """
    exp2 = pd.read_csv(exp2_oof_path)
    fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
    rows = []
    for _, row in baseline_df.iterrows():
        rid  = row[ID_COLUMN]
        lbl  = override_true_label.get(rid, row[TARGET_COLUMN]) if override_true_label else row[TARGET_COLUMN]
        pred = override_pred_label.get(rid, lbl) if override_pred_label else lbl
        fold = override_fold.get(rid, fold_map[rid]) if override_fold else fold_map[rid]
        prob = {c: round(1.0 / len(LABEL_ORDER), 4) for c in EXPECTED_PROB_COLS}
        r = {ID_COLUMN: rid, "Combined_Group": f"CG-{rid}",
             "True_Label": lbl, "Predicted_Label": pred, "Fold": fold,
             **prob}
        rows.append(r)
    ref = pd.DataFrame(rows)
    if drop_prob_col is not None:
        ref = ref.drop(columns=[drop_prob_col])
    if nan_prob_col is not None and nan_prob_col in ref.columns:
        ref.loc[ref.index[0], nan_prob_col] = float("nan")
    if inf_prob_col is not None and inf_prob_col in ref.columns:
        ref.loc[ref.index[0], inf_prob_col] = float("inf")
    p = tmp_path / "repro_ref.csv"
    ref.to_csv(p, index=False)
    return p


def _make_arm_a_oof(baseline_df: pd.DataFrame, exp2_oof_path: Path) -> pd.DataFrame:
    """Build a minimal Arm A OOF DataFrame with valid probabilities."""
    exp2 = pd.read_csv(exp2_oof_path)
    fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
    rows = []
    for _, row in baseline_df.iterrows():
        rid  = row[ID_COLUMN]
        lbl  = row[TARGET_COLUMN]
        prob = {c: round(1.0 / len(LABEL_ORDER), 4) for c in EXPECTED_PROB_COLS}
        rows.append({
            ID_COLUMN: rid, "Combined_Group": f"CG-{rid}",
            "True_Label": lbl, "Predicted_Label": lbl,
            "Fold": fold_map[rid], **prob,
        })
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest.fixture()
def baseline_df() -> pd.DataFrame:
    return _make_baseline_df(n=8)


@pytest.fixture()
def candidate_csv(tmp_path, baseline_df) -> Path:
    return _make_candidate_csv(baseline_df, tmp_path)


@pytest.fixture()
def exp2_oof_csv(tmp_path, baseline_df) -> Path:
    return _make_exp2_oof(baseline_df, tmp_path)


@pytest.fixture()
def repro_ref_csv(tmp_path, baseline_df, exp2_oof_csv) -> Path:
    return _make_repro_ref_csv(baseline_df, exp2_oof_csv, tmp_path)


@pytest.fixture()
def arm_a_oof(baseline_df, exp2_oof_csv) -> pd.DataFrame:
    return _make_arm_a_oof(baseline_df, exp2_oof_csv)


# ---------------------------------------------------------------------------
# Tests: _require_file
# ---------------------------------------------------------------------------

class TestRequireFile:
    def test_existing_file_does_not_raise(self, tmp_path):
        f = tmp_path / "exists.csv"
        f.write_text("col\nval\n")
        _require_file(f, "test file")

    def test_missing_file_raises_file_not_found(self, tmp_path):
        with pytest.raises(FileNotFoundError, match="mandatory input"):
            _require_file(tmp_path / "absent.csv", "test file")

    def test_error_message_contains_label(self, tmp_path):
        with pytest.raises(FileNotFoundError, match="fold reference"):
            _require_file(tmp_path / "absent2.csv", "fold reference")


# ---------------------------------------------------------------------------
# Tests: _require_columns
# ---------------------------------------------------------------------------

class TestRequireColumns:
    def test_all_present_passes(self):
        df = pd.DataFrame({"a": [1], "b": [2]})
        _require_columns(df, {"a", "b"}, "src")

    def test_missing_raises(self):
        df = pd.DataFrame({"a": [1]})
        with pytest.raises(ValueError, match="Required column"):
            _require_columns(df, {"a", "missing_col"}, "src")

    def test_error_names_missing_column(self):
        df = pd.DataFrame({"x": [1]})
        with pytest.raises(ValueError, match="missing_col"):
            _require_columns(df, {"missing_col"}, "src")


# ---------------------------------------------------------------------------
# Tests: load_and_filter_baseline
# ---------------------------------------------------------------------------

class TestLoadAndFilterBaseline:
    def test_returns_only_baseline_rows(self, candidate_csv):
        raw = pd.read_csv(candidate_csv)
        filtered = raw[raw["Record_Origin"] == BASELINE_ORIGIN]
        assert (filtered["Record_Origin"] == BASELINE_ORIGIN).all()

    def test_synthetic_rows_excluded(self, candidate_csv):
        raw = pd.read_csv(candidate_csv)
        assert "SYN-0001" not in raw[raw["Record_Origin"] == BASELINE_ORIGIN][ID_COLUMN].values

    def test_source_derived_rows_excluded(self, candidate_csv):
        raw = pd.read_csv(candidate_csv)
        assert "SRC-001" not in raw[raw["Record_Origin"] == BASELINE_ORIGIN][ID_COLUMN].values

    def test_missing_file_raises(self, tmp_path):
        with pytest.raises(FileNotFoundError):
            load_and_filter_baseline(tmp_path / "nonexistent.csv")

    def test_missing_record_origin_column_raises(self, tmp_path):
        df = pd.DataFrame({ID_COLUMN: ["A"], TEXT_COLUMN: ["text"],
                           TARGET_COLUMN: [LABEL_ORDER[0]], GROUP_COLUMN: ["g"]})
        p = tmp_path / "no_origin.csv"
        df.to_csv(p, index=False)
        with pytest.raises(ValueError, match="Record_Origin"):
            load_and_filter_baseline(p)

    def test_empty_baseline_raises(self, tmp_path):
        rows = [{ID_COLUMN: f"SYN-{i:04d}", TEXT_COLUMN: "text",
                 TARGET_COLUMN: LABEL_ORDER[0], GROUP_COLUMN: "G",
                 "Record_Origin": "New synthetic scenario"} for i in range(3)]
        p = tmp_path / "no_baseline.csv"
        pd.DataFrame(rows).to_csv(p, index=False)
        with pytest.raises(ValueError, match="0 unique Research_IDs"):
            load_and_filter_baseline(p)

    def test_invalid_label_raises_with_200_rows(self, tmp_path):
        rows = [{ID_COLUMN: f"BL-{i:03d}",
                 TEXT_COLUMN: f"Text {i}.",
                 TARGET_COLUMN: LABEL_ORDER[i % len(LABEL_ORDER)],
                 GROUP_COLUMN: f"G-{i}",
                 "Record_Origin": BASELINE_ORIGIN} for i in range(200)]
        rows[0][TARGET_COLUMN] = "Invalid Label"
        p = tmp_path / "bad.csv"
        pd.DataFrame(rows).to_csv(p, index=False)
        with pytest.raises(ValueError, match="[Ii]nvalid"):
            load_and_filter_baseline(p)


# ---------------------------------------------------------------------------
# Tests: production 200-row count enforcement
# ---------------------------------------------------------------------------

class TestProductionPopulationCount:
    """run_experiment4_arm_a enforces exactly 200 baseline records."""

    def test_population_check_rejects_wrong_count(self, tmp_path, baseline_df, exp2_oof_csv):
        """
        Supply a CSV with only 8 baseline rows.  The production entry point
        must reject it with a clear message about the expected count of 200.
        """
        candidate_path = _make_candidate_csv(baseline_df, tmp_path)
        out_dir = tmp_path / "run_out"

        with patch.object(ev4, "CANDIDATE_CSV_PATH", candidate_path), \
             patch.object(ev4, "EXP2_OOF_PATH", exp2_oof_csv):
            with pytest.raises(ValueError, match="200"):
                run_experiment4_arm_a(output_dir=out_dir)


# ---------------------------------------------------------------------------
# Tests: validate_fold_reference
# ---------------------------------------------------------------------------

class TestValidateFoldReference:
    def test_valid_reference_returns_fold_map(self, baseline_df, exp2_oof_csv):
        exp2_oof = pd.read_csv(exp2_oof_csv)
        fold_map = validate_fold_reference(baseline_df, exp2_oof)
        assert set(fold_map.keys()) == set(baseline_df[ID_COLUMN])
        assert set(fold_map.values()).issubset({1, 2, 3, 4, 5})

    def test_missing_true_label_column_raises(self, baseline_df, exp2_oof_csv):
        """True_Label is now mandatory in the fold reference."""
        oof = pd.read_csv(exp2_oof_csv)
        oof = oof.drop(columns=["True_Label"])
        with pytest.raises(ValueError, match="True_Label"):
            validate_fold_reference(baseline_df, oof)

    def test_missing_fold_column_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        with pytest.raises(ValueError, match="Fold"):
            validate_fold_reference(baseline_df, oof.drop(columns=["Fold"]))

    def test_missing_combined_group_column_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        with pytest.raises(ValueError, match="Combined_Group"):
            validate_fold_reference(baseline_df, oof.drop(columns=["Combined_Group"]))

    def test_id_missing_from_fold_reference_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        with pytest.raises(ValueError, match="not found in fold reference"):
            validate_fold_reference(baseline_df, oof.iloc[1:])

    def test_extra_id_in_fold_reference_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        extra_row = {ID_COLUMN: "EXTRA-001", "Combined_Group": "CG-EXTRA",
                     "Fold": 1, "True_Label": LABEL_ORDER[0], "Predicted_Label": LABEL_ORDER[0]}
        oof = pd.concat([oof, pd.DataFrame([extra_row])], ignore_index=True)
        with pytest.raises(ValueError, match="not in baseline"):
            validate_fold_reference(baseline_df, oof)

    def test_combined_group_in_multiple_folds_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        # First check blank CG is caught before multi-fold
        oof.loc[oof["Fold"] == 1, "Combined_Group"] = "SHARED"
        oof.loc[oof["Fold"] == 2, "Combined_Group"] = "SHARED"
        with pytest.raises(ValueError, match="multiple|fold"):
            validate_fold_reference(baseline_df, oof)

    def test_blank_combined_group_raises_before_multi_fold(self, baseline_df, exp2_oof_csv):
        """Blank Combined_Group must be rejected with a clear message."""
        oof = pd.read_csv(exp2_oof_csv)
        oof.iloc[0, oof.columns.get_loc("Combined_Group")] = "   "  # blank
        with pytest.raises(ValueError, match="blank Combined_Group"):
            validate_fold_reference(baseline_df, oof)

    def test_null_combined_group_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        oof.iloc[0, oof.columns.get_loc("Combined_Group")] = None
        with pytest.raises(ValueError, match="null.*Combined_Group|blank Combined_Group"):
            validate_fold_reference(baseline_df, oof)

    def test_label_mismatch_raises(self, baseline_df, exp2_oof_csv):
        """True_Label in fold reference must agree with baseline labels."""
        oof = pd.read_csv(exp2_oof_csv)
        oof.iloc[0, oof.columns.get_loc("True_Label")] = LABEL_ORDER[-1] + "-WRONG"
        with pytest.raises(ValueError, match="true labels"):
            validate_fold_reference(baseline_df, oof)

    def test_fold_values_not_1_to_5_raises(self, baseline_df, exp2_oof_csv):
        oof = pd.read_csv(exp2_oof_csv)
        oof["Fold"] = 99
        with pytest.raises(ValueError, match="1.5|fold"):
            validate_fold_reference(baseline_df, oof)


# ---------------------------------------------------------------------------
# Tests: Frozen fold and group separation in run_arm_a
# ---------------------------------------------------------------------------

class TestRunArmAFoldIntegrity:
    def test_each_record_evaluated_exactly_once(self, tmp_path, baseline_df, exp2_oof_csv):
        exp2 = pd.read_csv(exp2_oof_csv)
        fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
        cg_map   = dict(zip(exp2[ID_COLUMN], exp2["Combined_Group"]))
        df = baseline_df.copy()
        df["Combined_Group"] = df[ID_COLUMN].map(cg_map)

        result = run_arm_a(tmp_path / "arm_a", df, fold_map, cg_map)
        oof = result["oof_df"]
        assert len(oof) == len(df)
        assert oof["Research_ID"].nunique() == len(df)

    def test_no_combined_group_overlap_between_train_and_test(
        self, tmp_path, baseline_df, exp2_oof_csv
    ):
        exp2 = pd.read_csv(exp2_oof_csv)
        fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
        cg_map   = dict(zip(exp2[ID_COLUMN], exp2["Combined_Group"]))
        df = baseline_df.copy()
        df["Combined_Group"] = df[ID_COLUMN].map(cg_map)
        result = run_arm_a(tmp_path / "arm_a_sep", df, fold_map, cg_map)
        assert result is not None

    def test_forced_group_overlap_raises_assertion(
        self, tmp_path, baseline_df, exp2_oof_csv
    ):
        exp2 = pd.read_csv(exp2_oof_csv)
        fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
        cg_map_broken = {rid: "SINGLE-GROUP" for rid in exp2[ID_COLUMN]}
        df = baseline_df.copy()
        df["Combined_Group"] = df[ID_COLUMN].map(cg_map_broken)
        with pytest.raises(AssertionError, match="overlap"):
            run_arm_a(tmp_path / "arm_a_overlap", df, fold_map, cg_map_broken)


# ---------------------------------------------------------------------------
# Tests: Probability column ordering — meaningful controlled stub
# ---------------------------------------------------------------------------

class TestProbabilityColumnOrdering:
    """
    Use a controlled pipeline stub with deliberately reordered classes_ and
    distinct known probability values.  Assert each output probability column
    receives the correct class-specific value, not just that row sums are 1.
    """

    def _run_arm_a_with_stub(
        self,
        tmp_path: Path,
        baseline_df: pd.DataFrame,
        exp2_oof_csv: Path,
        stub_classes: list[str],
        stub_prob_row: list[float],
        stub_pred: str,
    ):
        """
        Patch build_pipeline_with_cleaner() to return a stub pipeline whose
        classifier always returns:
          - predict() → stub_pred for all test rows
          - predict_proba() → stub_prob_row for all test rows
          - classes_ → stub_classes (deliberately reordered)
        """
        exp2 = pd.read_csv(exp2_oof_csv)
        fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
        cg_map   = dict(zip(exp2[ID_COLUMN], exp2["Combined_Group"]))
        df = baseline_df.copy()
        df["Combined_Group"] = df[ID_COLUMN].map(cg_map)

        def _make_stub():
            mock_clf = MagicMock()
            mock_clf.classes_ = np.array(stub_classes)
            def _predict(X):
                return np.array([stub_pred] * len(X))
            def _predict_proba(X):
                return np.tile(stub_prob_row, (len(X), 1))
            mock_clf.predict.side_effect = _predict
            mock_clf.predict_proba.side_effect = _predict_proba

            mock_pipeline = MagicMock()
            mock_pipeline.named_steps = {"clf": mock_clf}
            mock_pipeline.predict.side_effect = _predict
            mock_pipeline.predict_proba.side_effect = _predict_proba
            return mock_pipeline

        arm_dir = tmp_path / "arm_stub"
        with patch("evaluate_experiment4.build_pipeline_with_cleaner", side_effect=_make_stub):
            result = run_arm_a(arm_dir, df, fold_map, cg_map)
        return result["oof_df"]

    def test_prob_columns_map_to_correct_class_value(
        self, tmp_path, baseline_df, exp2_oof_csv
    ):
        """
        Stub: classes_ = [L3, L0, L1, L2] (deliberately shuffled).
        Prob row = [0.1, 0.4, 0.3, 0.2] aligned to classes_ order.
        Expected output columns:
          prob_L0 = 0.4, prob_L1 = 0.3, prob_L2 = 0.2, prob_L3 = 0.1
        """
        L0, L1, L2, L3 = LABEL_ORDER
        stub_classes   = [L3, L0, L1, L2]
        stub_prob_row  = [0.1, 0.4, 0.3, 0.2]

        oof = self._run_arm_a_with_stub(
            tmp_path, baseline_df, exp2_oof_csv, stub_classes, stub_prob_row, L0
        )
        # Each label's prob column must receive the value from stub_prob_row
        # at the position corresponding to its index in stub_classes.
        expected = {
            _prob_col(L3): 0.1,
            _prob_col(L0): 0.4,
            _prob_col(L1): 0.3,
            _prob_col(L2): 0.2,
        }
        for col, expected_val in expected.items():
            assert col in oof.columns, f"Expected column '{col}' not found in OOF."
            actual_vals = oof[col].unique()
            assert len(actual_vals) == 1, f"Column '{col}' has more than one value: {actual_vals}"
            assert abs(actual_vals[0] - expected_val) < 1e-6, (
                f"Column '{col}': expected {expected_val}, got {actual_vals[0]}"
            )

    def test_prob_columns_are_non_negative(self, tmp_path, baseline_df, exp2_oof_csv):
        exp2 = pd.read_csv(exp2_oof_csv)
        fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))
        cg_map   = dict(zip(exp2[ID_COLUMN], exp2["Combined_Group"]))
        df = baseline_df.copy()
        df["Combined_Group"] = df[ID_COLUMN].map(cg_map)
        result = run_arm_a(tmp_path / "arm_a_neg", df, fold_map, cg_map)
        oof = result["oof_df"]
        prob_cols = [c for c in oof.columns if c.startswith("prob_")]
        assert (oof[prob_cols].fillna(0) >= 0).all().all()


# ---------------------------------------------------------------------------
# Tests: _validate_prob_matrix
# ---------------------------------------------------------------------------

class TestValidateProbMatrix:
    def _make_valid_df(self, n: int = 4) -> pd.DataFrame:
        """Uniform probability DataFrame with valid rows."""
        val = round(1.0 / len(EXPECTED_PROB_COLS), 4)
        return pd.DataFrame(
            {c: [val] * n for c in EXPECTED_PROB_COLS}
        )

    def test_valid_matrix_returns_array(self):
        df = self._make_valid_df()
        arr = _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)
        assert arr.shape == (4, len(EXPECTED_PROB_COLS))
        assert arr.dtype == float

    def test_missing_column_raises(self):
        df = self._make_valid_df()
        df = df.drop(columns=[EXPECTED_PROB_COLS[0]])
        with pytest.raises(ValueError, match="missing"):
            _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)

    def test_nan_value_raises(self):
        df = self._make_valid_df()
        df.iloc[0, 0] = float("nan")
        with pytest.raises(ValueError, match="NaN|non-finite"):
            _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)

    def test_inf_value_raises(self):
        df = self._make_valid_df()
        df.iloc[0, 0] = float("inf")
        with pytest.raises(ValueError, match="Inf|non-finite"):
            _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)

    def test_negative_value_raises(self):
        df = self._make_valid_df()
        df.iloc[0, 0] = -0.1
        with pytest.raises(ValueError, match=r"\[0, 1\]|outside"):
            _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)

    def test_value_above_one_raises(self):
        df = self._make_valid_df()
        df.iloc[0, 0] = 1.5
        with pytest.raises(ValueError, match=r"\[0, 1\]|outside"):
            _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)

    def test_row_sum_far_from_one_raises(self):
        # Set all values to 0 so row sums = 0.0 (way outside tolerance)
        df = pd.DataFrame({c: [0.0] * 4 for c in EXPECTED_PROB_COLS})
        with pytest.raises(ValueError, match="row|sum"):
            _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)

    def test_columns_aligned_by_name_not_position(self):
        """
        Provide a DataFrame with columns in reversed order.
        The validator must align by expected_cols order, not DataFrame column order.
        """
        rev_cols = list(reversed(EXPECTED_PROB_COLS))
        val = round(1.0 / len(EXPECTED_PROB_COLS), 4)
        df  = pd.DataFrame({c: [val] * 4 for c in rev_cols})
        # Should not raise — all values valid
        arr = _validate_prob_matrix(df, "test", EXPECTED_PROB_COLS)
        assert arr.shape[1] == len(EXPECTED_PROB_COLS)


# ---------------------------------------------------------------------------
# Tests: run_reproduction_check — all five PASS conditions
# ---------------------------------------------------------------------------

class TestRunReproductionCheck:
    def test_all_conditions_pass_gives_pass_status(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof, repro_ref_csv
    ):
        result = run_reproduction_check(
            arm_a_oof=arm_a_oof,
            repro_path=repro_ref_csv,
            output_path=tmp_path / "repro.json",
        )
        assert result["reproduction_status"] == "PASS"
        assert result["prediction_mismatches"]["count"] == 0
        assert result["true_label_mismatches"]["count"] == 0
        assert result["fold_mismatches"]["count"] == 0
        assert result["probabilities_within_tolerance"] is True

    def test_identical_predictions_but_mismatched_folds_gives_unresolved(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof
    ):
        """Same predicted labels but wrong fold → must be UNRESOLVED, not PASS."""
        # Build reference with one fold number changed
        first_rid = baseline_df[ID_COLUMN].iloc[0]
        repro = _make_repro_ref_csv(
            baseline_df, exp2_oof_csv, tmp_path,
            override_fold={first_rid: 99},
        )
        result = run_reproduction_check(
            arm_a_oof=arm_a_oof,
            repro_path=repro,
            output_path=tmp_path / "repro_fold.json",
        )
        assert result["reproduction_status"] == "UNRESOLVED"
        assert result["fold_mismatches"]["count"] > 0
        assert first_rid in result["fold_mismatches"]["ids"]

    def test_identical_predictions_but_mismatched_true_labels_gives_unresolved(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof
    ):
        """Same predicted labels but wrong true label in reference → UNRESOLVED."""
        first_rid = baseline_df[ID_COLUMN].iloc[0]
        # Use a different label than what baseline_df has
        current_lbl = baseline_df.set_index(ID_COLUMN).loc[first_rid, TARGET_COLUMN]
        other_lbl = next(l for l in LABEL_ORDER if l != current_lbl)
        repro = _make_repro_ref_csv(
            baseline_df, exp2_oof_csv, tmp_path,
            override_true_label={first_rid: other_lbl},
        )
        result = run_reproduction_check(
            arm_a_oof=arm_a_oof,
            repro_path=repro,
            output_path=tmp_path / "repro_true.json",
        )
        assert result["reproduction_status"] == "UNRESOLVED"
        assert result["true_label_mismatches"]["count"] > 0

    def test_probability_exceeding_tolerance_gives_unresolved(
        self, tmp_path, baseline_df, exp2_oof_csv
    ):
        """If max |prob diff| > PROB_TOLERANCE, reproduction must be UNRESOLVED."""
        exp2 = pd.read_csv(exp2_oof_csv)
        fold_map = dict(zip(exp2[ID_COLUMN], exp2["Fold"]))

        # Arm A OOF: equal uniform probabilities
        arm_a = _make_arm_a_oof(baseline_df, exp2_oof_csv)

        # Reference: one prob column shifted by 2 * PROB_TOLERANCE (beyond tolerance)
        ref_rows = []
        for _, row in baseline_df.iterrows():
            rid  = row[ID_COLUMN]
            lbl  = row[TARGET_COLUMN]
            prob = {c: round(1.0 / len(LABEL_ORDER), 4) for c in EXPECTED_PROB_COLS}
            # Shift first column up and last column down by 2x tolerance
            shift = PROB_TOLERANCE * 2
            prob[EXPECTED_PROB_COLS[0]]  = round(prob[EXPECTED_PROB_COLS[0]] + shift, 4)
            prob[EXPECTED_PROB_COLS[-1]] = round(prob[EXPECTED_PROB_COLS[-1]] - shift, 4)
            ref_rows.append({ID_COLUMN: rid, "Combined_Group": f"CG-{rid}",
                              "True_Label": lbl, "Predicted_Label": lbl,
                              "Fold": fold_map[rid], **prob})
        ref = pd.DataFrame(ref_rows)
        repro_path = tmp_path / "repro_prob.csv"
        ref.to_csv(repro_path, index=False)

        result = run_reproduction_check(
            arm_a_oof=arm_a,
            repro_path=repro_path,
            output_path=tmp_path / "repro_prob_r.json",
        )
        assert result["reproduction_status"] == "UNRESOLVED"
        assert result["max_abs_probability_diff"] is not None
        assert result["max_abs_probability_diff"] > PROB_TOLERANCE

    def test_missing_probability_column_in_reference_gives_clear_failure(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof
    ):
        """Missing prob column in reference must cause clear failure — not PASS."""
        repro = _make_repro_ref_csv(
            baseline_df, exp2_oof_csv, tmp_path,
            drop_prob_col=EXPECTED_PROB_COLS[0],
        )
        with pytest.raises(ValueError, match="missing|Required column"):
            run_reproduction_check(
                arm_a_oof=arm_a_oof,
                repro_path=repro,
                output_path=tmp_path / "repro_missing.json",
            )

    def test_nan_probability_in_reference_gives_invalid_input(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof
    ):
        """NaN in reference probability → INVALID_INPUT, not PASS."""
        repro = _make_repro_ref_csv(
            baseline_df, exp2_oof_csv, tmp_path,
            nan_prob_col=EXPECTED_PROB_COLS[0],
        )
        result = run_reproduction_check(
            arm_a_oof=arm_a_oof,
            repro_path=repro,
            output_path=tmp_path / "repro_nan.json",
        )
        assert result["reproduction_status"] == "INVALID_INPUT"
        assert result["pass_conditions"]["prob_validation_error"] is not None

    def test_infinite_probability_in_reference_gives_invalid_input(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof
    ):
        """Inf in reference probability → INVALID_INPUT, not PASS."""
        repro = _make_repro_ref_csv(
            baseline_df, exp2_oof_csv, tmp_path,
            inf_prob_col=EXPECTED_PROB_COLS[0],
        )
        result = run_reproduction_check(
            arm_a_oof=arm_a_oof,
            repro_path=repro,
            output_path=tmp_path / "repro_inf.json",
        )
        assert result["reproduction_status"] == "INVALID_INPUT"

    def test_prob_comparison_initialized_only_after_validation(
        self, tmp_path, baseline_df, exp2_oof_csv, arm_a_oof
    ):
        """
        max_abs_probability_diff must be None (not 0.0) when prob validation
        fails.  A 0.0 init would falsely imply a successful zero-difference result.
        """
        repro = _make_repro_ref_csv(
            baseline_df, exp2_oof_csv, tmp_path,
            nan_prob_col=EXPECTED_PROB_COLS[0],
        )
        result = run_reproduction_check(
            arm_a_oof=arm_a_oof,
            repro_path=repro,
            output_path=tmp_path / "repro_init.json",
        )
        assert result["max_abs_probability_diff"] is None


# ---------------------------------------------------------------------------
# Tests: nonempty explicit output_dir rejected
# ---------------------------------------------------------------------------

class TestOutputDirectoryProtection:
    def test_nonempty_explicit_output_dir_raises_before_writing(
        self, tmp_path, baseline_df, exp2_oof_csv
    ):
        """
        Supplying a nonempty explicit output_dir must raise ValueError
        before any files are written.
        """
        nonempty_dir = tmp_path / "existing_run"
        nonempty_dir.mkdir()
        (nonempty_dir / "some_file.csv").write_text("data")

        candidate_path = _make_candidate_csv(baseline_df, tmp_path)
        with patch.object(ev4, "CANDIDATE_CSV_PATH", candidate_path), \
             patch.object(ev4, "EXP2_OOF_PATH", exp2_oof_csv):
            with pytest.raises(ValueError, match="not empty|nonempty|non-existent"):
                run_experiment4_arm_a(output_dir=nonempty_dir)

    def test_empty_explicit_output_dir_is_accepted(
        self, tmp_path, baseline_df, exp2_oof_csv
    ):
        """An empty explicit output_dir must not raise the occupation check."""
        empty_dir = tmp_path / "empty_run"
        empty_dir.mkdir()
        assert not _dir_is_occupied(empty_dir)

        candidate_path = _make_candidate_csv(baseline_df, tmp_path)
        with patch.object(ev4, "CANDIDATE_CSV_PATH", candidate_path), \
             patch.object(ev4, "EXP2_OOF_PATH", exp2_oof_csv):
            # Should raise on the 200-row check (not on directory occupation)
            with pytest.raises(ValueError, match="200"):
                run_experiment4_arm_a(output_dir=empty_dir)


# ---------------------------------------------------------------------------
# Tests: compute_per_class_metrics — FPR, undefined, reason preserved
# ---------------------------------------------------------------------------

class TestComputePerClassMetrics:
    def _make_labels(self):
        y_true = (
            [LABEL_ORDER[0]] * 5 + [LABEL_ORDER[1]] * 5
            + [LABEL_ORDER[2]] * 5 + [LABEL_ORDER[3]] * 5
        )
        y_pred = y_true[:]
        y_pred[0] = LABEL_ORDER[1]
        y_pred[5] = LABEL_ORDER[0]
        return y_true, y_pred

    def test_fpr_is_fp_over_fp_plus_tn(self):
        y_true, y_pred = self._make_labels()
        results = compute_per_class_metrics(y_true, y_pred)
        for lbl in LABEL_ORDER:
            m = results[lbl]
            if m["FPR_defined"]:
                expected = round(m["FP"] / (m["FP"] + m["TN"]), 4)
                assert m["FPR"] == expected

    def test_support_equals_tp_plus_fn(self):
        y_true, y_pred = self._make_labels()
        results = compute_per_class_metrics(y_true, y_pred)
        for lbl in LABEL_ORDER:
            assert results[lbl]["Support"] == results[lbl]["TP"] + results[lbl]["FN"]

    def test_reason_preserved_when_metric_undefined(self):
        """compute_per_class_metrics must preserve reason strings in returned dict."""
        y_true = [LABEL_ORDER[0]] * 4 + [LABEL_ORDER[1]] * 4
        y_pred = [LABEL_ORDER[0]] * 8  # LABEL_ORDER[1] never predicted
        results = compute_per_class_metrics(y_true, y_pred)
        lbl1 = results[LABEL_ORDER[1]]
        if lbl1["TP"] == 0 and lbl1["FP"] == 0:
            assert lbl1["Precision"] == "undefined"
            assert lbl1["Precision_defined"] is False
            # Reason must be present and non-None
            assert lbl1["Precision_reason"] is not None
            assert len(lbl1["Precision_reason"]) > 0

    def test_perfect_predictions_all_defined_and_no_reason(self):
        y_true = [lbl for lbl in LABEL_ORDER for _ in range(4)]
        y_pred = y_true[:]
        results = compute_per_class_metrics(y_true, y_pred)
        for lbl in LABEL_ORDER:
            m = results[lbl]
            assert m["Precision_defined"]
            assert m["Precision_reason"] is None
            assert m["Recall_defined"]
            assert m["Recall_reason"] is None
            assert m["F1_defined"]
            assert m["F1_reason"] is None
            assert m["FPR_defined"]
            assert m["FPR_reason"] is None
            assert m["Precision"] == 1.0
            assert m["Recall"] == 1.0
            assert m["F1"] == 1.0
            assert m["FPR"] == 0.0


# ---------------------------------------------------------------------------
# Tests: _safe_macro undefined propagation
# ---------------------------------------------------------------------------

class TestSafeMacro:
    def _all_defined(self):
        return {lbl: {"F1": 0.8, "F1_defined": True} for lbl in LABEL_ORDER}

    def _one_undefined(self):
        d = {lbl: {"F1": 0.8, "F1_defined": True} for lbl in LABEL_ORDER}
        d[LABEL_ORDER[0]]["F1"] = "undefined"
        d[LABEL_ORDER[0]]["F1_defined"] = False
        return d

    def test_all_defined_returns_numeric(self):
        out = _safe_macro(self._all_defined(), "F1")
        assert isinstance(out["value"], float)

    def test_one_undefined_returns_undefined(self):
        out = _safe_macro(self._one_undefined(), "F1")
        assert out["value"] == "undefined"
        assert LABEL_ORDER[0] in out["undefined_classes"]

    def test_undefined_macro_has_reason_and_undefined_classes(self):
        out = _safe_macro(self._one_undefined(), "F1")
        assert "reason" in out
        assert "undefined_classes" in out

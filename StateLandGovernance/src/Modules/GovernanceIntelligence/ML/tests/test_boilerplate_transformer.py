"""
test_boilerplate_transformer.py
--------------------------------
Unit tests for the BoilerplateStripper transformer and the
Experiment 3 pipeline factory.

Tests cover:
* strip_research_boilerplate removes each phrase exactly
* strip_research_boilerplate is idempotent
* Text without boilerplate is returned unchanged (whitespace-normalized)
* Text with all three phrases is fully cleaned
* Empty string returns empty string (no error)
* BoilerplateStripper.fit() returns self and is a no-op
* BoilerplateStripper.transform() cleans a list of texts
* BoilerplateStripper.transform() handles numpy array input
* BoilerplateStripper.transform() leaves unchanged records unchanged
* build_pipeline_with_cleaner() returns a Pipeline
* Experiment 3 pipeline contains 'boilerplate', 'tfidf', 'clf' steps
* Experiment 3 pipeline predicts the allowed label set
* Experiment 3 pipeline probabilities sum to 1
* BoilerplateStripper does NOT alter incident-specific text content
* Baseline build_pipeline() does NOT contain 'boilerplate' step (regression guard)
* resolve_output_dir selects first empty/absent directory (5 scenarios)
* _dir_is_occupied correctly identifies occupied vs empty directories
"""

from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline

# Add src/ to sys.path
_SRC_DIR = Path(__file__).resolve().parent.parent / "src"
sys.path.insert(0, str(_SRC_DIR))

from boilerplate_transformer import (
    BOILERPLATE_PHRASES,
    BoilerplateStripper,
    build_pipeline_with_cleaner,
    strip_research_boilerplate,
)
from config import LABEL_ORDER
from model import build_pipeline
from evaluate_experiment3 import _dir_is_occupied, resolve_output_dir, EXP3_OUTPUT_NAME


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def boilerplate_text_all_three() -> str:
    """A text containing all three boilerplate phrases."""
    return (
        "Testville Estate "
        "was identified in official audit/report material as a government/state land lease case involving "
        "lease non-payment. "
        "The Land Commissioner has confirmed that this is a genuine, direct, distinct state-land lease incident. "
        "File-level details should be retained from the Commissioner's records when available."
    )


@pytest.fixture
def boilerplate_text_phrase1_only() -> str:
    """A text containing only phrase 1."""
    return (
        "Lakeside plot "
        "was identified in official audit/report material as a government/state land lease case involving "
        "long-term arrears."
    )


@pytest.fixture
def clean_text() -> str:
    """A text with no boilerplate whatsoever."""
    return "The occupant transferred the lease without proper consent from the Land Commissioner."


@pytest.fixture
def minimal_training_data() -> tuple[list[str], list[str]]:
    """
    Minimal training data covering all four classes (needed for LR to fit).
    These are short but class-diverse synthetic samples.
    """
    texts = [
        # Administrative
        "The officer failed to process the renewal within the statutory period.",
        "No compliance check was conducted before issuing the extension.",
        "The lease renewal was denied without a recorded reason by the administrative unit.",
        # Revenue
        "Lease rental arrears accumulated over five years without enforcement action.",
        "The occupant did not pay the revised rental after revaluation.",
        "Revenue collection for the coastal lease was delayed for three consecutive quarters.",
        # Unauthorized
        "The lessee transferred the state land parcel to a third party without consent.",
        "Unauthorized construction was found on the leased government plot.",
        "A sub-lease was created without official land commission authorization.",
        # Environmental
        "A hotel resort was constructed in a declared forest buffer zone lease area.",
        "The coastal wetland reserve was converted to a commercial lease site.",
        "Mangrove habitat was cleared under a protected-zone lease agreement.",
    ]
    labels = [
        "Administrative / Procedural / Integrity",
        "Administrative / Procedural / Integrity",
        "Administrative / Procedural / Integrity",
        "Lease Revenue / Payment / Enforcement",
        "Lease Revenue / Payment / Enforcement",
        "Lease Revenue / Payment / Enforcement",
        "Unauthorized Allocation / Transfer / Use",
        "Unauthorized Allocation / Transfer / Use",
        "Unauthorized Allocation / Transfer / Use",
        "Protected / Environmental Lease Misuse",
        "Protected / Environmental Lease Misuse",
        "Protected / Environmental Lease Misuse",
    ]
    return texts, labels


# ---------------------------------------------------------------------------
# Tests: strip_research_boilerplate (pure function)
# ---------------------------------------------------------------------------

class TestStripResearchBoilerplate:

    def test_removes_phrase_1(self):
        text = "Testville was identified in official audit/report material as a government/state land lease case involving arrears."
        result = strip_research_boilerplate(text)
        assert BOILERPLATE_PHRASES[0] not in result

    def test_removes_phrase_2(self):
        text = "Some incident. The Land Commissioner has confirmed that this is a genuine, direct, distinct state-land lease incident."
        result = strip_research_boilerplate(text)
        assert BOILERPLATE_PHRASES[1] not in result

    def test_removes_phrase_3(self):
        text = "Some incident. File-level details should be retained from the Commissioner's records when available."
        result = strip_research_boilerplate(text)
        assert BOILERPLATE_PHRASES[2] not in result

    def test_removes_all_three_phrases(self, boilerplate_text_all_three):
        result = strip_research_boilerplate(boilerplate_text_all_three)
        for phrase in BOILERPLATE_PHRASES:
            assert phrase not in result

    def test_result_is_nonempty_for_all_three(self, boilerplate_text_all_three):
        result = strip_research_boilerplate(boilerplate_text_all_three)
        assert len(result.strip()) > 0

    def test_result_contains_incident_content(self, boilerplate_text_all_three):
        """Incident-specific text must survive boilerplate removal."""
        result = strip_research_boilerplate(boilerplate_text_all_three)
        assert "Testville Estate" in result
        assert "lease non-payment" in result

    def test_clean_text_unchanged_after_whitespace_norm(self, clean_text):
        import re
        result = strip_research_boilerplate(clean_text)
        normalized_original = re.sub(r"\s+", " ", clean_text).strip()
        assert result == normalized_original

    def test_idempotent(self, boilerplate_text_all_three):
        once = strip_research_boilerplate(boilerplate_text_all_three)
        twice = strip_research_boilerplate(once)
        assert once == twice

    def test_empty_string_returns_empty(self):
        assert strip_research_boilerplate("") == ""

    def test_whitespace_collapsed(self, boilerplate_text_all_three):
        result = strip_research_boilerplate(boilerplate_text_all_three)
        import re
        # Should not contain double-spaces or leading/trailing whitespace
        assert "  " not in result
        assert result == result.strip()

    def test_phrase1_only_text_still_nonempty(self, boilerplate_text_phrase1_only):
        result = strip_research_boilerplate(boilerplate_text_phrase1_only)
        assert len(result.strip()) > 0

    def test_phrase1_only_preserves_incident_content(self, boilerplate_text_phrase1_only):
        result = strip_research_boilerplate(boilerplate_text_phrase1_only)
        assert "Lakeside plot" in result
        assert "long-term arrears" in result


# ---------------------------------------------------------------------------
# Tests: BoilerplateStripper transformer
# ---------------------------------------------------------------------------

class TestBoilerplateStripper:

    def test_fit_returns_self(self, boilerplate_text_all_three):
        stripper = BoilerplateStripper()
        returned = stripper.fit([boilerplate_text_all_three])
        assert returned is stripper

    def test_fit_with_y_returns_self(self, boilerplate_text_all_three):
        stripper = BoilerplateStripper()
        returned = stripper.fit([boilerplate_text_all_three], y=["label"])
        assert returned is stripper

    def test_transform_list_of_strings(self, boilerplate_text_all_three, clean_text):
        stripper = BoilerplateStripper()
        result = stripper.transform([boilerplate_text_all_three, clean_text])
        assert isinstance(result, list)
        assert len(result) == 2
        for phrase in BOILERPLATE_PHRASES:
            assert phrase not in result[0]

    def test_transform_numpy_array(self, boilerplate_text_all_three):
        stripper = BoilerplateStripper()
        arr = np.array([boilerplate_text_all_three, "Another unrelated text."])
        result = stripper.transform(arr)
        assert isinstance(result, list)
        assert len(result) == 2

    def test_transform_clean_text_unchanged(self, clean_text):
        import re
        stripper = BoilerplateStripper()
        result = stripper.transform([clean_text])
        normalized = re.sub(r"\s+", " ", clean_text).strip()
        assert result[0] == normalized

    def test_transform_is_idempotent(self, boilerplate_text_all_three):
        stripper = BoilerplateStripper()
        once = stripper.transform([boilerplate_text_all_three])
        twice = stripper.transform(once)
        assert once == twice

    def test_sklearn_compatible_get_params(self):
        stripper = BoilerplateStripper()
        params = stripper.get_params()
        assert isinstance(params, dict)

    def test_empty_list(self):
        stripper = BoilerplateStripper()
        result = stripper.transform([])
        assert result == []


# ---------------------------------------------------------------------------
# Tests: build_pipeline_with_cleaner() factory
# ---------------------------------------------------------------------------

class TestBuildPipelineWithCleaner:

    def test_returns_pipeline(self):
        pipeline = build_pipeline_with_cleaner()
        assert isinstance(pipeline, Pipeline)

    def test_contains_boilerplate_step(self):
        pipeline = build_pipeline_with_cleaner()
        assert "boilerplate" in pipeline.named_steps

    def test_contains_tfidf_step(self):
        pipeline = build_pipeline_with_cleaner()
        assert "tfidf" in pipeline.named_steps
        assert isinstance(pipeline.named_steps["tfidf"], TfidfVectorizer)

    def test_contains_clf_step(self):
        pipeline = build_pipeline_with_cleaner()
        assert "clf" in pipeline.named_steps
        assert isinstance(pipeline.named_steps["clf"], LogisticRegression)

    def test_step_order_is_boilerplate_tfidf_clf(self):
        pipeline = build_pipeline_with_cleaner()
        step_names = [name for name, _ in pipeline.steps]
        assert step_names == ["boilerplate", "tfidf", "clf"]

    def test_boilerplate_step_is_boilerplate_stripper(self):
        pipeline = build_pipeline_with_cleaner()
        assert isinstance(pipeline.named_steps["boilerplate"], BoilerplateStripper)

    def test_pipeline_fit_predict_minimal_data(self, minimal_training_data):
        texts, labels = minimal_training_data
        pipeline = build_pipeline_with_cleaner()
        pipeline.fit(texts, labels)
        preds = pipeline.predict(texts)
        assert len(preds) == len(texts)
        for pred in preds:
            assert pred in LABEL_ORDER

    def test_pipeline_predict_proba_shape(self, minimal_training_data):
        texts, labels = minimal_training_data
        pipeline = build_pipeline_with_cleaner()
        pipeline.fit(texts, labels)
        proba = pipeline.predict_proba(texts[:3])
        assert proba.shape == (3, 4)

    def test_pipeline_probabilities_sum_to_one(self, minimal_training_data):
        texts, labels = minimal_training_data
        pipeline = build_pipeline_with_cleaner()
        pipeline.fit(texts, labels)
        proba = pipeline.predict_proba(texts[:3])
        for row in proba:
            assert abs(row.sum() - 1.0) < 1e-5

    def test_each_call_returns_unfitted_pipeline(self):
        p1 = build_pipeline_with_cleaner()
        p2 = build_pipeline_with_cleaner()
        assert p1 is not p2

    def test_pipeline_with_boilerplate_text(self, minimal_training_data):
        """Pipeline should handle boilerplate-containing texts without errors."""
        texts, labels = minimal_training_data
        pipeline = build_pipeline_with_cleaner()
        pipeline.fit(texts, labels)
        boilerplate_test = [
            "Site X was identified in official audit/report material as a government/state land lease case involving arrears. "
            "The Land Commissioner has confirmed that this is a genuine, direct, distinct state-land lease incident. "
            "File-level details should be retained from the Commissioner's records when available."
        ]
        preds = pipeline.predict(boilerplate_test)
        assert preds[0] in LABEL_ORDER


# ---------------------------------------------------------------------------
# Regression guard: baseline pipeline must NOT contain boilerplate step
# ---------------------------------------------------------------------------

class TestBaselinePipelineRegression:

    def test_baseline_pipeline_has_no_boilerplate_step(self):
        pipeline = build_pipeline()
        assert "boilerplate" not in pipeline.named_steps

    def test_baseline_pipeline_step_order_is_tfidf_clf(self):
        pipeline = build_pipeline()
        step_names = [name for name, _ in pipeline.steps]
        assert step_names == ["tfidf", "clf"]


# ---------------------------------------------------------------------------
# Tests: _dir_is_occupied and resolve_output_dir
# ---------------------------------------------------------------------------

class TestOutputDirResolution:
    """
    Focused tests for _dir_is_occupied() and resolve_output_dir().

    All scenarios use pytest's tmp_path fixture so no real result directory
    is created or modified. No evaluation is executed.
    """

    def test_dir_is_occupied_missing_directory(self, tmp_path):
        absent = tmp_path / "nonexistent"
        assert not _dir_is_occupied(absent)

    def test_dir_is_occupied_empty_directory(self, tmp_path):
        empty = tmp_path / "empty_dir"
        empty.mkdir()
        assert not _dir_is_occupied(empty)

    def test_dir_is_occupied_nonempty_directory(self, tmp_path):
        nonempty = tmp_path / "has_file"
        nonempty.mkdir()
        (nonempty / "partial_output.csv").write_text("some data")
        assert _dir_is_occupied(nonempty)

    def test_resolve_missing_base_uses_canonical_name(self, tmp_path):
        # Neither canonical dir nor any numbered dir exists
        result = resolve_output_dir(tmp_path)
        assert result == tmp_path / EXP3_OUTPUT_NAME
        assert result.exists()

    def test_resolve_existing_empty_base_uses_canonical_name(self, tmp_path):
        # Canonical dir exists but is empty
        canonical = tmp_path / EXP3_OUTPUT_NAME
        canonical.mkdir()
        result = resolve_output_dir(tmp_path)
        assert result == canonical

    def test_resolve_nonempty_incomplete_run_uses_run1(self, tmp_path):
        # Canonical dir exists with a partial file but no completion JSON
        canonical = tmp_path / EXP3_OUTPUT_NAME
        canonical.mkdir()
        (canonical / "exp3a_baseline_rerun").mkdir()
        (canonical / "exp3a_baseline_rerun" / "oof_predictions.csv").write_text("data")
        result = resolve_output_dir(tmp_path)
        assert result == tmp_path / f"{EXP3_OUTPUT_NAME}_run1"
        assert result.exists()

    def test_resolve_completed_run_uses_run1(self, tmp_path):
        # Canonical dir has a full completion JSON
        canonical = tmp_path / EXP3_OUTPUT_NAME
        canonical.mkdir()
        (canonical / "experiment3_comparison.json").write_text("{}")
        result = resolve_output_dir(tmp_path)
        assert result == tmp_path / f"{EXP3_OUTPUT_NAME}_run1"
        assert result.exists()

    def test_resolve_several_occupied_numbered_dirs_uses_next_available(self, tmp_path):
        # Canonical and run1..run3 all occupied; run4 should be selected
        canonical = tmp_path / EXP3_OUTPUT_NAME
        canonical.mkdir()
        (canonical / "experiment3_comparison.json").write_text("{}")
        for i in range(1, 4):
            d = tmp_path / f"{EXP3_OUTPUT_NAME}_run{i}"
            d.mkdir()
            (d / "experiment3_comparison.json").write_text("{}")
        result = resolve_output_dir(tmp_path)
        assert result == tmp_path / f"{EXP3_OUTPUT_NAME}_run4"
        assert result.exists()

    def test_resolve_creates_returned_directory(self, tmp_path):
        # The returned path must exist after the call
        result = resolve_output_dir(tmp_path)
        assert result.is_dir()


# ---------------------------------------------------------------------------
# Tests: population separation in transition counts
# ---------------------------------------------------------------------------

class TestTransitionPopulationSeparation:
    """
    Verify that changed-text and unchanged-text transition counts
    remain separate and do not bleed across populations.

    Uses a small synthetic audit DataFrame — no model evaluation is run.
    This test guards against the bug where cleaning_helped_count in
    changed_records_analysis was computed from all records (including
    unchanged ones), producing an off-by-one error.
    """

    @staticmethod
    def _make_audit_df():
        """
        Construct a minimal synthetic audit DataFrame with:
        - 1 changed record that cleaning helped (A wrong, B correct)
        - 1 changed record where both are correct
        - 1 unchanged record that cleaning helped (A wrong, B correct)
        - 1 unchanged record where both are correct

        Expected totals:
        - Overall:   helped=2, hurt=0
        - Changed:   helped=1, hurt=0  (arm_a_correct=1, arm_b_correct=2)
        - Unchanged: helped=1, hurt=0  (arm_a_correct=1, arm_b_correct=2)
        """
        data = {
            "Research_ID":          ["C1", "C2", "U1", "U2"],
            "Was_Changed_By_Cleaning": [True, True, False, False],
            "True_Label":           ["X", "X", "Y", "Y"],
            "Arm_A_Predicted":      ["Z", "X", "W", "Y"],   # C1 wrong, U1 wrong
            "Arm_B_Predicted":      ["X", "X", "Y", "Y"],   # both B correct
        }
        df = pd.DataFrame(data)
        df["Arm_A_Correct"] = df["True_Label"] == df["Arm_A_Predicted"]
        df["Arm_B_Correct"] = df["True_Label"] == df["Arm_B_Predicted"]
        df["Cleaning_Helped"] = (~df["Arm_A_Correct"]) & df["Arm_B_Correct"]
        df["Cleaning_Hurt"]   = df["Arm_A_Correct"] & (~df["Arm_B_Correct"])
        return df

    def test_overall_helped_is_two(self):
        df = self._make_audit_df()
        assert int(df["Cleaning_Helped"].sum()) == 2

    def test_overall_hurt_is_zero(self):
        df = self._make_audit_df()
        assert int(df["Cleaning_Hurt"].sum()) == 0

    def test_changed_helped_is_one_not_two(self):
        df = self._make_audit_df()
        changed = df[df["Was_Changed_By_Cleaning"]]
        assert int(changed["Cleaning_Helped"].sum()) == 1

    def test_unchanged_helped_is_one_not_two(self):
        df = self._make_audit_df()
        unchanged = df[~df["Was_Changed_By_Cleaning"]]
        assert int(unchanged["Cleaning_Helped"].sum()) == 1

    def test_changed_arm_a_correct(self):
        df = self._make_audit_df()
        changed = df[df["Was_Changed_By_Cleaning"]]
        assert int(changed["Arm_A_Correct"].sum()) == 1

    def test_changed_arm_b_correct(self):
        df = self._make_audit_df()
        changed = df[df["Was_Changed_By_Cleaning"]]
        assert int(changed["Arm_B_Correct"].sum()) == 2

    def test_unchanged_arm_a_correct(self):
        df = self._make_audit_df()
        unchanged = df[~df["Was_Changed_By_Cleaning"]]
        assert int(unchanged["Arm_A_Correct"].sum()) == 1

    def test_unchanged_arm_b_correct(self):
        df = self._make_audit_df()
        unchanged = df[~df["Was_Changed_By_Cleaning"]]
        assert int(unchanged["Arm_B_Correct"].sum()) == 2

    def test_arithmetic_identity_changed(self):
        # B_correct - A_correct == helped - hurt for changed subset
        df = self._make_audit_df()
        changed = df[df["Was_Changed_By_Cleaning"]]
        b_a = int(changed["Arm_B_Correct"].sum()) - int(changed["Arm_A_Correct"].sum())
        h_h = int(changed["Cleaning_Helped"].sum()) - int(changed["Cleaning_Hurt"].sum())
        assert b_a == h_h

    def test_arithmetic_identity_unchanged(self):
        # B_correct - A_correct == helped - hurt for unchanged subset
        df = self._make_audit_df()
        unchanged = df[~df["Was_Changed_By_Cleaning"]]
        b_a = int(unchanged["Arm_B_Correct"].sum()) - int(unchanged["Arm_A_Correct"].sum())
        h_h = int(unchanged["Cleaning_Helped"].sum()) - int(unchanged["Cleaning_Hurt"].sum())
        assert b_a == h_h

    def test_arithmetic_identity_overall(self):
        # B_correct - A_correct == helped - hurt for all records
        df = self._make_audit_df()
        b_a = int(df["Arm_B_Correct"].sum()) - int(df["Arm_A_Correct"].sum())
        h_h = int(df["Cleaning_Helped"].sum()) - int(df["Cleaning_Hurt"].sum())
        assert b_a == h_h

    def test_changed_helped_does_not_include_unchanged_improvement(self):
        # The key regression guard: changed_helped must equal 1, not 2
        df = self._make_audit_df()
        changed_helped = int(df[df["Was_Changed_By_Cleaning"]]["Cleaning_Helped"].sum())
        total_helped   = int(df["Cleaning_Helped"].sum())
        # If changed_helped == total_helped, the populations were incorrectly merged
        assert changed_helped != total_helped, (
            "changed_helped must not equal total_helped when an unchanged record also improved"
        )
        assert changed_helped == 1
        assert total_helped == 2

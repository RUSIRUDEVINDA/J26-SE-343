"""
test_classifier.py
------------------
Automated tests for the Component 4 GovernanceIntelligence text-classification
ML module.

Tests cover:
* Required-column validation
* Invalid target-label rejection
* Missing / blank text rejection
* Allowed label taxonomy
* Pipeline construction
* Pipeline contains TF-IDF
* Pipeline contains Logistic Regression
* Simple fit/predict smoke test
* Prediction belongs to allowed label set
* predict_proba returns four probabilities
* Probabilities approximately sum to 1

Tests do NOT:
* Fabricate benchmark results
* Assert specific accuracy thresholds
* Assert specific confusion matrix values
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

# Add src/ to sys.path so tests can import module code
_SRC_DIR = Path(__file__).resolve().parent.parent / "src"
sys.path.insert(0, str(_SRC_DIR))

from config import LABEL_ORDER, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN, ID_COLUMN
from data_loader import (
    load_and_validate,
    get_feature_arrays,
    get_class_distribution,
    REQUIRED_COLUMNS,
    ALLOWED_LABELS,
)
from model import build_pipeline


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def minimal_valid_df() -> pd.DataFrame:
    """
    Minimal valid DataFrame with one record per class.
    """
    records = [
        {
            ID_COLUMN    : "T-001",
            TEXT_COLUMN  : "An officer accepted a payment to approve the lease application.",
            TARGET_COLUMN: "Administrative / Procedural / Integrity",
            GROUP_COLUMN : "group-A",
        },
        {
            ID_COLUMN    : "T-002",
            TEXT_COLUMN  : "Lease rent has not been paid for three consecutive years.",
            TARGET_COLUMN: "Lease Revenue / Payment / Enforcement",
            GROUP_COLUMN : "group-B",
        },
        {
            ID_COLUMN    : "T-003",
            TEXT_COLUMN  : "The government land was transferred to another company without approval.",
            TARGET_COLUMN: "Unauthorized Allocation / Transfer / Use",
            GROUP_COLUMN : "group-C",
        },
        {
            ID_COLUMN    : "T-004",
            TEXT_COLUMN  : "Protected coastal land was leased for a private resort development.",
            TARGET_COLUMN: "Protected / Environmental Lease Misuse",
            GROUP_COLUMN : "group-D",
        },
    ]
    return pd.DataFrame(records)


@pytest.fixture
def trained_pipeline(minimal_valid_df):
    """A pipeline fitted on the minimal valid dataset."""
    pipeline = build_pipeline()
    X = minimal_valid_df[TEXT_COLUMN].tolist()
    y = minimal_valid_df[TARGET_COLUMN].tolist()
    pipeline.fit(X, y)
    return pipeline


# ---------------------------------------------------------------------------
# Label taxonomy tests
# ---------------------------------------------------------------------------

class TestAllowedLabelTaxonomy:
    def test_exactly_four_labels_in_label_order(self):
        assert len(LABEL_ORDER) == 4

    def test_administrative_label_present(self):
        assert "Administrative / Procedural / Integrity" in LABEL_ORDER

    def test_revenue_label_present(self):
        assert "Lease Revenue / Payment / Enforcement" in LABEL_ORDER

    def test_unauthorized_label_present(self):
        assert "Unauthorized Allocation / Transfer / Use" in LABEL_ORDER

    def test_environmental_label_present(self):
        assert "Protected / Environmental Lease Misuse" in LABEL_ORDER

    def test_label_order_matches_allowed_labels(self):
        assert set(LABEL_ORDER) == ALLOWED_LABELS


# ---------------------------------------------------------------------------
# Column validation tests
# ---------------------------------------------------------------------------

class TestRequiredColumnValidation:
    def test_missing_text_column_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.drop(columns=[TEXT_COLUMN])
        with pytest.raises(ValueError, match="missing required columns"):
            from data_loader import _assert_columns_present
            _assert_columns_present(bad_df, Path("test.csv"))

    def test_missing_target_column_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.drop(columns=[TARGET_COLUMN])
        with pytest.raises(ValueError, match="missing required columns"):
            from data_loader import _assert_columns_present
            _assert_columns_present(bad_df, Path("test.csv"))

    def test_missing_group_column_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.drop(columns=[GROUP_COLUMN])
        with pytest.raises(ValueError, match="missing required columns"):
            from data_loader import _assert_columns_present
            _assert_columns_present(bad_df, Path("test.csv"))

    def test_all_required_columns_present_passes(self, minimal_valid_df):
        from data_loader import _assert_columns_present
        # Should not raise
        _assert_columns_present(minimal_valid_df, Path("test.csv"))


# ---------------------------------------------------------------------------
# Target label rejection tests
# ---------------------------------------------------------------------------

class TestInvalidTargetLabelRejection:
    def test_invalid_label_raises_value_error(self, minimal_valid_df):
        bad_df = minimal_valid_df.copy()
        bad_df.loc[0, TARGET_COLUMN] = "Corruption / Criminal Conduct"
        with pytest.raises(ValueError, match="unexpected label"):
            from data_loader import _assert_valid_labels
            _assert_valid_labels(bad_df)

    def test_all_valid_labels_pass(self, minimal_valid_df):
        from data_loader import _assert_valid_labels
        # Should not raise
        _assert_valid_labels(minimal_valid_df)

    def test_null_label_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.copy()
        bad_df.loc[0, TARGET_COLUMN] = None
        with pytest.raises(ValueError, match="null"):
            from data_loader import _assert_no_null_labels
            _assert_no_null_labels(bad_df)


# ---------------------------------------------------------------------------
# Missing / blank text rejection tests
# ---------------------------------------------------------------------------

class TestMissingTextRejection:
    def test_null_text_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.copy()
        bad_df.loc[0, TEXT_COLUMN] = None
        with pytest.raises(ValueError, match="null"):
            from data_loader import _assert_no_null_text
            _assert_no_null_text(bad_df)

    def test_blank_text_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.copy()
        bad_df.loc[0, TEXT_COLUMN] = "   "
        with pytest.raises(ValueError, match="blank"):
            from data_loader import _assert_no_blank_text
            _assert_no_blank_text(bad_df)

    def test_empty_string_raises(self, minimal_valid_df):
        bad_df = minimal_valid_df.copy()
        bad_df.loc[0, TEXT_COLUMN] = ""
        with pytest.raises(ValueError, match="blank"):
            from data_loader import _assert_no_blank_text
            _assert_no_blank_text(bad_df)

    def test_valid_text_passes(self, minimal_valid_df):
        from data_loader import _assert_no_null_text, _assert_no_blank_text
        # Should not raise
        _assert_no_null_text(minimal_valid_df)
        _assert_no_blank_text(minimal_valid_df)


# ---------------------------------------------------------------------------
# Pipeline construction tests
# ---------------------------------------------------------------------------

class TestPipelineConstruction:
    def test_build_pipeline_returns_pipeline(self):
        pipeline = build_pipeline()
        assert isinstance(pipeline, Pipeline)

    def test_pipeline_contains_tfidf_step(self):
        pipeline = build_pipeline()
        assert "tfidf" in pipeline.named_steps
        assert isinstance(pipeline.named_steps["tfidf"], TfidfVectorizer)

    def test_pipeline_contains_lr_step(self):
        pipeline = build_pipeline()
        assert "clf" in pipeline.named_steps
        assert isinstance(pipeline.named_steps["clf"], LogisticRegression)

    def test_build_pipeline_returns_new_instance_each_time(self):
        p1 = build_pipeline()
        p2 = build_pipeline()
        assert p1 is not p2
        assert p1.named_steps["tfidf"] is not p2.named_steps["tfidf"]

    def test_tfidf_config_applied(self):
        pipeline = build_pipeline()
        tfidf = pipeline.named_steps["tfidf"]
        assert tfidf.lowercase is True
        assert tfidf.stop_words == "english"
        assert tfidf.ngram_range == (1, 2)
        assert tfidf.sublinear_tf is True

    def test_lr_config_applied(self):
        pipeline = build_pipeline()
        clf = pipeline.named_steps["clf"]
        assert clf.max_iter == 2000
        assert clf.class_weight == "balanced"
        assert clf.random_state == 42
        assert clf.C == 1.0


# ---------------------------------------------------------------------------
# Smoke / fit-predict tests
# ---------------------------------------------------------------------------

class TestFitPredictSmoke:
    def test_pipeline_fits_without_error(self, minimal_valid_df):
        pipeline = build_pipeline()
        X = minimal_valid_df[TEXT_COLUMN].tolist()
        y = minimal_valid_df[TARGET_COLUMN].tolist()
        pipeline.fit(X, y)  # Should not raise

    def test_predict_returns_list_of_correct_length(self, trained_pipeline, minimal_valid_df):
        X = minimal_valid_df[TEXT_COLUMN].tolist()
        preds = trained_pipeline.predict(X)
        assert len(preds) == len(X)

    def test_prediction_belongs_to_allowed_label_set(self, trained_pipeline):
        test_texts = [
            "An officer accepted a bribe to expedite the lease approval.",
            "Lease rentals remain unpaid for five years.",
            "State land was sold to a private company without any procedure.",
            "A hotel was built inside a protected forest reserve on leased land.",
        ]
        preds = trained_pipeline.predict(test_texts)
        for pred in preds:
            assert pred in ALLOWED_LABELS, f"Unexpected prediction: {pred}"

    def test_predict_proba_returns_four_probabilities(self, trained_pipeline):
        test_text = ["Government land leased without approval."]
        proba = trained_pipeline.predict_proba(test_text)
        assert proba.shape[1] == 4, (
            f"Expected 4 class probabilities, got {proba.shape[1]}"
        )

    def test_predict_proba_sums_to_approximately_one(self, trained_pipeline):
        test_texts = [
            "The official requested a payment to approve the lease.",
            "Lease revenue was not collected for several years.",
        ]
        proba = trained_pipeline.predict_proba(test_texts)
        for row in proba:
            total = float(np.sum(row))
            assert abs(total - 1.0) < 1e-5, (
                f"Probabilities do not sum to 1: {total}"
            )

    def test_predict_proba_all_non_negative(self, trained_pipeline):
        test_text = ["Environmental damage caused by unregulated leased activity."]
        proba = trained_pipeline.predict_proba(test_text)
        assert np.all(proba >= 0.0), "All probabilities must be non-negative."


# ---------------------------------------------------------------------------
# Real dataset smoke test (requires CSV to be present)
# ---------------------------------------------------------------------------

class TestRealDatasetSmoke:
    def test_real_dataset_loads_and_validates(self):
        """Load the real 200-row CSV and confirm it passes all validation."""
        try:
            df = load_and_validate()
        except FileNotFoundError:
            pytest.skip("Real dataset CSV not found — skipping integration smoke test.")
        assert len(df) > 0

    def test_real_dataset_has_exactly_four_classes(self):
        try:
            df = load_and_validate()
        except FileNotFoundError:
            pytest.skip("Real dataset CSV not found.")
        unique_labels = set(df[TARGET_COLUMN].unique())
        assert unique_labels == ALLOWED_LABELS

    def test_real_dataset_feature_arrays_have_consistent_lengths(self):
        try:
            df = load_and_validate()
        except FileNotFoundError:
            pytest.skip("Real dataset CSV not found.")
        X, y, groups = get_feature_arrays(df)
        assert len(X) == len(y) == len(groups) == len(df)

    def test_class_distribution_dict_covers_all_labels(self):
        try:
            df = load_and_validate()
        except FileNotFoundError:
            pytest.skip("Real dataset CSV not found.")
        dist = get_class_distribution(df)
        assert set(dist.keys()) == set(LABEL_ORDER)
        assert all(isinstance(v, int) and v > 0 for v in dist.values())

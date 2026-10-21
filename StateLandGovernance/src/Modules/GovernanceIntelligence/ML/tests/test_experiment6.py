"""Fictional-fixture tests for the one-time Experiment 6 evaluator."""

from __future__ import annotations

import json
import sys
from datetime import date
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

SRC = Path(__file__).resolve().parent.parent / "src"
sys.path.insert(0, str(SRC))

import evaluate_experiment6 as exp6


LABELS = exp6.LABEL_ORDER


def review_fixture() -> pd.DataFrame:
    rows = []
    for index in range(25):
        label = LABELS[index % 4]
        rows.append({
            exp6.FAMILY_COLUMN: f"PF-{index:02d}",
            "Proposed_Label": label,
            "Approved_Label": "",
            "Reviewer": "Human Reviewer",
            "Review_Date": "10/11/2026",
            "Review_Decision": "Approved",
            "Variant_Consistency": "Consistent",
            "Review_Notes": "Human-reviewed fictional family.",
        })
    return pd.DataFrame(rows)


def protocol_fixture() -> tuple[dict, dict]:
    protocol = {"experiment": 5, "arms": ["arm_a", "arm_b", "arm_c", "arm_d", "arm_e"], "class_order": LABELS, "random_seed": 42}
    metadata = {
        "arm": "arm_e",
        "pipeline": "word + character TF-IDF",
        "probability_class_order": LABELS,
        "random_seed": 42,
        "real_training_weight": 1.0,
        "synthetic_training": "all variants, each family total weight 1",
        "character_tfidf": {"analyzer": "char_wb", "ngram_range": [3, 5], "lowercase": True, "sublinear_tf": True, "min_df": 1},
    }
    return protocol, metadata


def oof_fixture() -> pd.DataFrame:
    rows = []
    for family_index, label in enumerate(LABELS):
        for variant in range(1, 4):
            predicted = label
            probabilities = [0.05] * 4
            probabilities[LABELS.index(predicted)] = 0.85
            row = {
                exp6.ID_COLUMN: f"T-{family_index}-{variant}",
                exp6.FAMILY_COLUMN: f"PF-{family_index}",
                exp6.VARIANT_COLUMN: variant,
                "True_Label": label,
                "Predicted_Label": predicted,
                "evaluation_type": "synthetic prospective evaluation",
            }
            row.update(dict(zip(exp6.PROBABILITY_COLUMNS, probabilities)))
            rows.append(row)
    return pd.DataFrame(rows)


def test_incomplete_human_review_is_rejected():
    review = review_fixture()
    review.loc[0, "Reviewer"] = ""
    with pytest.raises(ValueError, match="blank Reviewer"):
        exp6.validate_review_decisions(review, date(2026, 10, 11))


def test_unknown_decision_is_rejected():
    review = review_fixture()
    review.loc[0, "Review_Decision"] = "allowed"
    with pytest.raises(ValueError, match="Unknown"):
        exp6.validate_review_decisions(review, date(2026, 10, 11))


def test_future_review_date_is_rejected():
    review = review_fixture()
    review.loc[0, "Review_Date"] = "10/12/2026"
    with pytest.raises(ValueError, match="after evaluation date"):
        exp6.validate_review_decisions(review, date(2026, 10, 11))


def test_label_change_requires_valid_approved_label():
    review = review_fixture()
    review.loc[0, "Review_Decision"] = "Label_Change"
    with pytest.raises(ValueError, match="valid Approved_Label"):
        exp6.validate_review_decisions(review, date(2026, 10, 11))
    review.loc[0, "Approved_Label"] = LABELS[1]
    eligible, _ = exp6.validate_review_decisions(review, date(2026, 10, 11))
    assert eligible.loc[eligible[exp6.FAMILY_COLUMN] == "PF-00", "Final_Label"].item() == LABELS[1]


def test_approved_populated_label_must_equal_proposed():
    review = review_fixture()
    review.loc[0, "Approved_Label"] = LABELS[1]
    with pytest.raises(ValueError, match="different from Proposed_Label"):
        exp6.validate_review_decisions(review, date(2026, 10, 11))


def test_rejected_and_needs_discussion_are_excluded():
    review = review_fixture()
    review.loc[0, "Review_Decision"] = "Rejected"
    review.loc[1, "Review_Decision"] = "Needs_Discussion"
    eligible, excluded = exp6.validate_review_decisions(review, date(2026, 10, 11))
    assert len(eligible) == 23
    assert set(excluded["Review_Decision"]) == {"Rejected", "Needs_Discussion"}
    assert excluded["Final_Label"].isna().all()


def prospective_source_fixture():
    review = review_fixture().copy()
    template_rows = []
    candidate_rows = []
    for family_index, review_row in review.reset_index(drop=True).iterrows():
        family_id = review_row[exp6.FAMILY_COLUMN]
        template = {exp6.FAMILY_COLUMN: family_id, "Proposed_Label": review_row["Proposed_Label"]}
        for variant in range(1, 4):
            research_id = f"P-{family_index}-{variant}"
            text = f"fictional prospective {family_id} variant {variant}"
            template[f"Research_ID_V{variant}"] = research_id
            template[f"Canonical_English_Text_V{variant}"] = text
            candidate_rows.append({
                exp6.ID_COLUMN: research_id,
                exp6.TEXT_COLUMN: text,
                exp6.LABEL_COLUMN: review_row["Proposed_Label"],
                "Record_Origin": exp6.SYNTHETIC_ORIGIN,
                exp6.FAMILY_COLUMN: family_id,
                exp6.VARIANT_COLUMN: variant,
            })
        template_rows.append(template)
    review["Final_Label"] = review["Proposed_Label"]
    return review, pd.DataFrame(template_rows), pd.DataFrame(candidate_rows)


def test_exact_text_id_family_variant_matching():
    review, template, candidate = prospective_source_fixture()
    eligible, excluded = exp6.validate_prospective_records(review, template, candidate, set(), set())
    assert len(eligible) == 75
    assert excluded.empty
    candidate.loc[0, exp6.TEXT_COLUMN] = "changed"
    with pytest.raises(ValueError, match="exact-text mismatch"):
        exp6.validate_prospective_records(review, template, candidate, set(), set())


def test_development_family_overlap_is_rejected():
    review, template, candidate = prospective_source_fixture()
    with pytest.raises(ValueError, match="families overlap"):
        exp6.validate_prospective_records(review, template, candidate, {"PF-00"}, set())


def test_experiment5_oof_overlap_is_rejected():
    review, template, candidate = prospective_source_fixture()
    with pytest.raises(ValueError, match="entered an Experiment 5 OOF"):
        exp6.validate_prospective_records(review, template, candidate, set(), {"P-0-1"})


def test_source_derived_family_overlap_is_rejected():
    review, template, candidate = prospective_source_fixture()
    extra = candidate.iloc[[0]].copy()
    extra[exp6.ID_COLUMN] = "SRC-1"
    extra["Record_Origin"] = exp6.SOURCE_ORIGIN
    candidate = pd.concat([candidate, extra], ignore_index=True)
    with pytest.raises(ValueError, match="source-derived"):
        exp6.validate_prospective_records(review, template, candidate, set(), set())


def test_frozen_arm_e_configuration_reconstruction_and_hash_stability():
    protocol, metadata = protocol_fixture()
    first = exp6.reconstruct_frozen_arm_e_configuration(protocol, metadata)
    second = exp6.reconstruct_frozen_arm_e_configuration(json.loads(json.dumps(protocol)), json.loads(json.dumps(metadata)))
    assert first["feature_union"]["transformers"] == ["word", "character"]
    assert first["sample_weight_policy"]["synthetic_variant"] == pytest.approx(1 / 3)
    assert exp6.canonical_hash(first) == exp6.canonical_hash(second)


def test_configuration_rejects_character_parameter_change():
    protocol, metadata = protocol_fixture()
    metadata["character_tfidf"]["ngram_range"] = [2, 5]
    with pytest.raises(ValueError, match="character TF-IDF"):
        exp6.reconstruct_frozen_arm_e_configuration(protocol, metadata)


def training_fixture():
    baseline_rows = []
    candidate_rows = []
    for index in range(200):
        row = {
            exp6.ID_COLUMN: f"R-{index:03d}",
            exp6.TEXT_COLUMN: f"real complaint {index}",
            exp6.LABEL_COLUMN: LABELS[index % 4],
            exp6.config.GROUP_COLUMN: f"G-{index:03d}",
        }
        baseline_rows.append(row)
        candidate_rows.append({**row, "Record_Origin": exp6.BASELINE_ORIGIN, exp6.FAMILY_COLUMN: "", exp6.VARIANT_COLUMN: ""})
    review_rows = []
    for family_index in range(75):
        family_id = f"D-{family_index:03d}"
        label = LABELS[family_index % 4]
        review_rows.append({exp6.FAMILY_COLUMN: family_id, "Review_Decision": "Approved", "Approved_Label": "", "Proposed_Label": label})
        for variant in range(1, 4):
            candidate_rows.append({
                exp6.ID_COLUMN: f"D-{family_index:03d}-{variant}",
                exp6.TEXT_COLUMN: f"development synthetic {family_index} variant {variant}",
                exp6.LABEL_COLUMN: label,
                exp6.config.GROUP_COLUMN: "",
                "Record_Origin": exp6.SYNTHETIC_ORIGIN,
                exp6.FAMILY_COLUMN: family_id,
                exp6.VARIANT_COLUMN: variant,
            })
    return pd.DataFrame(baseline_rows), pd.DataFrame(candidate_rows), pd.DataFrame(review_rows)


def test_training_population_membership_and_prospective_exclusion():
    baseline, candidate, development_review = training_fixture()
    training = exp6.build_training_population(baseline, candidate, development_review, {"PF-1"})
    assert len(training) == 425
    assert training["population_origin"].value_counts().to_dict() == {"development_synthetic": 225, "real_baseline": 200}
    assert not training[exp6.FAMILY_COLUMN].eq("PF-1").any()
    assert not training["population_origin"].eq(exp6.SOURCE_ORIGIN).any()


def test_family_balanced_weights():
    training = pd.DataFrame({"population_origin": ["real_baseline"] * 2 + ["development_synthetic"] * 3})
    assert np.allclose(exp6.family_balanced_weights(training), [1.0, 1.0, 1 / 3, 1 / 3, 1 / 3])


def test_sample_weights_reach_logistic_regression_fit():
    class Spy:
        kwargs = None

        def fit(self, texts, labels, **kwargs):
            self.kwargs = kwargs
            return self

    spy = Spy()
    weights = np.array([1.0, 1 / 3])
    exp6.fit_pipeline_once(spy, ["real", "synthetic"], [LABELS[0], LABELS[1]], weights)
    assert set(spy.kwargs) == {"clf__sample_weight"}
    assert np.array_equal(spy.kwargs["clf__sample_weight"], weights)


def test_probability_alignment_and_validation():
    raw = np.array([[0.4, 0.3, 0.2, 0.1]])
    aligned = exp6.align_probabilities(list(reversed(LABELS)), raw)
    assert np.array_equal(aligned, np.array([[0.1, 0.2, 0.3, 0.4]]))
    result = exp6.validate_probabilities(aligned, [LABELS[3]])
    assert result["argmax_agreement"]


def test_record_and_family_metrics():
    predictions = oof_fixture()
    metrics, per_class, _ = exp6.calculate_record_metrics(predictions)
    family_rows, family_metrics = exp6.calculate_family_predictions(predictions)
    assert metrics["accuracy"] == 1.0
    assert metrics["macro_f1"] == 1.0
    assert set(per_class["recall"]) == {1.0}
    assert family_metrics["family_majority_accuracy"] == 1.0
    assert family_metrics["families_all_three_variants_correct"] == 4
    assert not family_rows["Majority_Tie"].any()


def test_deterministic_family_tie_handling():
    predictions = oof_fixture()
    family = predictions[exp6.FAMILY_COLUMN].eq("PF-0")
    indices = predictions.index[family].tolist()
    predictions.loc[indices[0], "Predicted_Label"] = LABELS[0]
    predictions.loc[indices[1], "Predicted_Label"] = LABELS[1]
    predictions.loc[indices[2], "Predicted_Label"] = LABELS[2]
    predictions.loc[indices, exp6.PROBABILITY_COLUMNS] = 0.25
    rows, metrics = exp6.calculate_family_predictions(predictions)
    tied = rows[rows[exp6.FAMILY_COLUMN] == "PF-0"].iloc[0]
    assert tied["Majority_Tie"]
    assert tied["Family_Majority_Prediction"] == LABELS[0]
    assert metrics["families_with_majority_ties"] == 1


@pytest.mark.parametrize("value,passes", [(0.8000001, True), (0.80, False), (0.7999999, False)])
def test_promotion_accuracy_boundary_is_strict(value, passes):
    metrics = {"accuracy": value, "macro_f1": 0.9, "evaluation_type": "synthetic prospective evaluation"}
    per_class = pd.DataFrame({"support": [1] * 4, "recall": [1.0] * 4})
    probability = {"finite": True, "inside_unit_interval": True, "row_sums_within_tolerance": True, "argmax_agreement": True}
    result = exp6.promotion_decision(metrics, per_class, probability, True)
    assert result["checks"]["accuracy_strictly_greater_than_0_80"] is passes


def test_zero_recall_represented_class_fails_promotion():
    metrics = {"accuracy": 0.9, "macro_f1": 0.9, "evaluation_type": "synthetic prospective evaluation"}
    per_class = pd.DataFrame({"support": [1] * 4, "recall": [1.0, 1.0, 0.0, 1.0]})
    probability = {"finite": True, "inside_unit_interval": True, "row_sums_within_tolerance": True, "argmax_agreement": True}
    result = exp6.promotion_decision(metrics, per_class, probability, True)
    assert not result["all_criteria_passed"]


def test_output_overwrite_prevention(tmp_path):
    path = tmp_path / "experiment6"
    path.mkdir()
    with pytest.raises(FileExistsError, match="already exists"):
        exp6.ensure_new_output_directory(path)


def test_file_hash_preservation_and_models_v1_analogue(tmp_path):
    protected = tmp_path / "models-v1-fictional.joblib"
    protected.write_bytes(b"fictional immutable model")
    before = exp6.sha256_file(protected)
    exp6.canonical_hash({"read_only": True})
    assert exp6.sha256_file(protected) == before


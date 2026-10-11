"""Focused fictional-fixture tests for Experiment 5."""

from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

SRC = Path(__file__).resolve().parent.parent / "src"
sys.path.insert(0, str(SRC))

import evaluate_experiment5 as exp5


LABELS = exp5.LABEL_ORDER


def synthetic_fixture(families_per_class: int = 5) -> pd.DataFrame:
    rows = []
    counter = 0
    for label_index, label in enumerate(LABELS):
        for family_number in range(families_per_class):
            family_id = f"F-{label_index}-{family_number}"
            for variant in (1, 2, 3):
                counter += 1
                rows.append({
                    exp5.ID_COLUMN: f"S-{counter:03d}",
                    exp5.TEXT_COLUMN: f"fictional complaint {family_id} variant {variant}",
                    exp5.TARGET_COLUMN: label,
                    "Synthetic_Family_ID": family_id,
                    "Synthetic_Variant": f"V{variant}",
                    "evaluation_fold": 1,
                })
    return pd.DataFrame(rows)


def oof_fixture(prefix: str = "R") -> pd.DataFrame:
    rows = []
    for index, label in enumerate(LABELS):
        probabilities = [0.05] * 4
        probabilities[index] = 0.85
        row = {
            exp5.ID_COLUMN: f"{prefix}-{index}",
            "True_Label": label,
            "Predicted_Label": label,
            "Fold": (index % 2) + 1,
            "synthetic_family_id": f"F-{index}",
        }
        row.update(dict(zip(exp5.PROBABILITY_COLUMNS, probabilities)))
        rows.append(row)
    return pd.DataFrame(rows)


def candidate_manifest_fixture() -> dict:
    real = pd.DataFrame([
        {
            exp5.ID_COLUMN: "R-1", exp5.TARGET_COLUMN: LABELS[0],
            "Record_Origin": exp5.BASELINE_ORIGIN, "Synthetic_Family_ID": "",
            "Synthetic_Variant": "", "evaluation_fold": 1, "Combined_Group": "CG-1",
        }
    ])
    approved = pd.DataFrame([
        {
            exp5.ID_COLUMN: f"S-{variant}", exp5.TARGET_COLUMN: LABELS[1],
            "Record_Origin": exp5.SYNTHETIC_ORIGIN, "Synthetic_Family_ID": "F-1",
            "Synthetic_Variant": variant, "evaluation_fold": 2,
        }
        for variant in (1, 2, 3)
    ])
    source = pd.DataFrame([
        {
            exp5.ID_COLUMN: "SRC-1", exp5.TARGET_COLUMN: LABELS[2],
            "Record_Origin": exp5.SOURCE_ORIGIN, "Synthetic_Family_ID": "",
            "Synthetic_Variant": "",
        }
    ])
    unreviewed = pd.DataFrame([
        {
            exp5.ID_COLUMN: "S-U1", exp5.TARGET_COLUMN: LABELS[3],
            "Record_Origin": exp5.SYNTHETIC_ORIGIN, "Synthetic_Family_ID": "F-U",
            "Synthetic_Variant": 1,
        }
    ])
    candidate = pd.concat([real.drop(columns=["evaluation_fold", "Combined_Group"]), approved.drop(columns=["evaluation_fold"]), source, unreviewed], ignore_index=True)
    return {
        "candidate": candidate,
        "baseline": real,
        "approved": approved,
        "approved_ids": {"F-1"},
        "synthetic_fold_map": {"F-1": 2},
    }


def test_baseline_population_exact_comparison_detects_text_change():
    left = pd.DataFrame([{exp5.ID_COLUMN: "R1", exp5.TEXT_COLUMN: "one", exp5.TARGET_COLUMN: LABELS[0], exp5.GROUP_COLUMN: "G1"}])
    right = left.copy()
    assert left.equals(right)
    right.loc[0, exp5.TEXT_COLUMN] = "changed"
    assert not left.equals(right)


def test_population_manifest_selects_only_real_and_approved():
    population, folds = exp5.build_manifests(candidate_manifest_fixture())
    assert set(folds[exp5.ID_COLUMN]) == {"R-1", "S-1", "S-2", "S-3"}
    assert not population.loc[population[exp5.ID_COLUMN] == "SRC-1", "included_in_experiment"].item()
    assert not population.loc[population[exp5.ID_COLUMN] == "S-U1", "included_in_experiment"].item()
    assert population.loc[population[exp5.ID_COLUMN] == "S-U1", "exclusion_reason"].item() == "synthetic family unreviewed"


def test_each_approved_family_has_three_unique_variants_fixture():
    frame = synthetic_fixture()
    assert frame.groupby("Synthetic_Family_ID").size().eq(3).all()
    assert frame.groupby("Synthetic_Family_ID")["Synthetic_Variant"].nunique().eq(3).all()


def test_multilabel_family_is_rejected():
    frame = synthetic_fixture()
    family = frame["Synthetic_Family_ID"].iloc[0]
    frame.loc[frame["Synthetic_Family_ID"].eq(family).idxmax(), exp5.TARGET_COLUMN] = LABELS[1]
    with pytest.raises(ValueError, match="multiple labels"):
        exp5.assign_synthetic_family_folds(frame)


def test_fold_assignment_is_deterministic_and_contains_families():
    frame = synthetic_fixture()
    first = exp5.assign_synthetic_family_folds(frame, seed=42)
    second = exp5.assign_synthetic_family_folds(frame.sample(frac=1, random_state=7), seed=42)
    assert first == second
    frame["fold"] = frame["Synthetic_Family_ID"].map(first)
    assert frame.groupby("Synthetic_Family_ID")["fold"].nunique().eq(1).all()
    family_rows = frame.drop_duplicates("Synthetic_Family_ID")
    assert family_rows.groupby("fold")[exp5.TARGET_COLUMN].nunique().eq(4).all()


def test_combined_group_overlap_is_rejected():
    real_train = pd.DataFrame([{exp5.ID_COLUMN: "R1", "Combined_Group": "G"}])
    real_eval = pd.DataFrame([{exp5.ID_COLUMN: "R2", "Combined_Group": "G"}])
    synthetic_train = synthetic_fixture(1).iloc[:3]
    synthetic_eval = synthetic_fixture(1).iloc[3:6]
    with pytest.raises(ValueError, match="Combined_Group"):
        exp5.validate_no_train_evaluation_overlap(real_train, real_eval, synthetic_train, synthetic_eval)


def test_synthetic_family_overlap_is_rejected():
    real_train = pd.DataFrame([{exp5.ID_COLUMN: "R1", "Combined_Group": "G1"}])
    real_eval = pd.DataFrame([{exp5.ID_COLUMN: "R2", "Combined_Group": "G2"}])
    synthetic_train = pd.DataFrame([{exp5.ID_COLUMN: "S1", "Synthetic_Family_ID": "F1"}])
    synthetic_eval = pd.DataFrame([{exp5.ID_COLUMN: "S2", "Synthetic_Family_ID": "F1"}])
    with pytest.raises(ValueError, match="family"):
        exp5.validate_no_train_evaluation_overlap(real_train, real_eval, synthetic_train, synthetic_eval)


def test_single_variant_selection_uses_lowest_variant():
    frame = synthetic_fixture(1).sample(frac=1, random_state=3)
    selected = exp5.select_one_variant_per_family(frame)
    assert len(selected) == 4
    assert set(selected["Synthetic_Variant"]) == {"V1"}


def test_family_balanced_weights_total_one_per_family():
    frame = synthetic_fixture(1)
    weights = exp5.family_balanced_weights(2, frame)
    assert np.array_equal(weights[:2], np.ones(2))
    frame = frame.copy()
    frame["weight"] = weights[2:]
    assert np.allclose(frame.groupby("Synthetic_Family_ID")["weight"].sum(), 1.0)


def test_sample_weights_are_routed_to_classifier_fit():
    class SpyPipeline:
        kwargs = None

        def fit(self, texts, labels, **kwargs):
            self.kwargs = kwargs
            return self

    spy = SpyPipeline()
    weights = np.array([1.0, 1.0 / 3.0])
    exp5.fit_pipeline(spy, ["a", "b"], [LABELS[0], LABELS[1]], weights)
    assert set(spy.kwargs) == {"clf__sample_weight"}
    assert np.array_equal(spy.kwargs["clf__sample_weight"], weights)


def test_probability_mapping_uses_classes_order():
    classes = list(reversed(LABELS))
    raw = np.array([[0.4, 0.3, 0.2, 0.1]])
    aligned = exp5.align_probabilities(classes, raw)
    assert np.array_equal(aligned, np.array([[0.1, 0.2, 0.3, 0.4]]))


@pytest.mark.parametrize("matrix", [
    np.array([[np.nan, 0.2, 0.3, 0.5]]),
    np.array([[-0.1, 0.2, 0.3, 0.6]]),
    np.array([[0.1, 0.2, 0.3, 0.5]]),
])
def test_invalid_probabilities_are_rejected(matrix):
    with pytest.raises(ValueError, match="Probability"):
        exp5.validate_probabilities(matrix, [LABELS[int(np.nanargmax(np.nan_to_num(matrix, nan=-1)))]] )


def test_prediction_must_equal_max_probability_class():
    matrix = np.array([[0.7, 0.1, 0.1, 0.1]])
    with pytest.raises(ValueError, match="maximum-probability"):
        exp5.validate_probabilities(matrix, [LABELS[1]])


def test_real_synthetic_combined_metrics_remain_separate():
    real = oof_fixture("R")
    synthetic = oof_fixture("S")
    synthetic.loc[0, "Predicted_Label"] = LABELS[1]
    combined = pd.concat([real, synthetic], ignore_index=True)
    real_metrics, _, _ = exp5.calculate_metrics(real)
    synthetic_metrics, _, _ = exp5.calculate_metrics(synthetic)
    combined_metrics, _, _ = exp5.calculate_metrics(combined)
    assert real_metrics["support"] == 4
    assert synthetic_metrics["support"] == 4
    assert combined_metrics["support"] == 8
    assert real_metrics["accuracy"] != synthetic_metrics["accuracy"]


def test_family_majority_metrics_and_correct_distribution():
    rows = []
    for family_index, label in enumerate(LABELS):
        for variant in range(3):
            predicted = label if not (family_index == 0 and variant == 0) else LABELS[1]
            probabilities = [0.05] * 4
            probabilities[LABELS.index(predicted)] = 0.85
            row = {exp5.ID_COLUMN: f"S-{family_index}-{variant}", "synthetic_family_id": f"F-{family_index}", "True_Label": label, "Predicted_Label": predicted, "Fold": 1}
            row.update(dict(zip(exp5.PROBABILITY_COLUMNS, probabilities)))
            rows.append(row)
    metrics = exp5.calculate_family_metrics(pd.DataFrame(rows))
    assert metrics["family_majority_accuracy"] == 1.0
    assert metrics["families_all_three_variants_correct"] == 3
    assert metrics["correct_variants_distribution"] == {"0": 0, "1": 0, "2": 1, "3": 3}


def test_paired_comparison_aligns_by_research_id():
    reference = oof_fixture("R")
    candidate = reference.sample(frac=1, random_state=8).reset_index(drop=True)
    candidate.loc[candidate[exp5.ID_COLUMN] == "R-0", "Predicted_Label"] = LABELS[1]
    summary, _ = exp5.paired_comparison(reference, candidate)
    assert summary["hurt"] == 1
    assert summary["helped"] == 0
    assert summary["net_gain"] == -1


def test_paired_comparison_rejects_duplicate_or_missing_ids():
    reference = oof_fixture("R")
    duplicate = pd.concat([reference, reference.iloc[[0]]], ignore_index=True)
    with pytest.raises(ValueError, match="duplicate"):
        exp5.paired_comparison(reference, duplicate)
    with pytest.raises(ValueError, match="sets differ"):
        exp5.paired_comparison(reference, reference.iloc[:-1])


def test_paired_comparison_rejects_label_and_fold_mismatch():
    reference = oof_fixture("R")
    label_mismatch = reference.copy()
    label_mismatch.loc[0, "True_Label"] = LABELS[1]
    with pytest.raises(ValueError, match="label mismatch"):
        exp5.paired_comparison(reference, label_mismatch)
    fold_mismatch = reference.copy()
    fold_mismatch.loc[0, "Fold"] = 5
    with pytest.raises(ValueError, match="fold mismatch"):
        exp5.paired_comparison(reference, fold_mismatch)


def test_output_directory_overwrite_protection(tmp_path):
    occupied = tmp_path / "experiment5"
    occupied.mkdir()
    (occupied / "evidence.txt").write_text("keep", encoding="utf-8")
    with pytest.raises(FileExistsError, match="refusing to overwrite"):
        exp5.ensure_new_output_directory(occupied)
    assert (occupied / "evidence.txt").read_text(encoding="utf-8") == "keep"


def test_source_file_hash_remains_unchanged_after_read(tmp_path):
    source = tmp_path / "fictional.csv"
    source.write_bytes(b"Research_ID,text\nR1,fictional\n")
    before = exp5.sha256_file(source)
    pd.read_csv(source)
    assert exp5.sha256_file(source) == before


def test_arm_a_reproduction_accepts_class_ordered_probabilities():
    actual = oof_fixture("R")
    reference = actual.copy()
    for column in exp5.PROBABILITY_COLUMNS:
        reference[column] = reference[column].round(4)
    result = exp5.validate_arm_a_reproduction(actual, reference)
    assert result["status"] == "PASS"
    assert result["prediction_mismatches"] == 0


def test_arm_a_reproduction_rejects_prediction_change():
    actual = oof_fixture("R")
    reference = actual.copy()
    reference.loc[0, "Predicted_Label"] = LABELS[1]
    with pytest.raises(ValueError, match="Predicted_Label"):
        exp5.validate_arm_a_reproduction(actual, reference)


def test_arm_metadata_explicitly_forbids_model_save():
    source = (SRC / "evaluate_experiment5.py").read_text(encoding="utf-8")
    assert '"model_saved": False' in source
    assert "joblib.dump" not in source


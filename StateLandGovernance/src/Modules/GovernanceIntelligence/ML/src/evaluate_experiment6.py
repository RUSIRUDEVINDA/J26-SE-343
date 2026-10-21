"""One-time prospective synthetic evaluation of frozen Experiment 5 Arm E.

The prospective population is used exactly once for final prediction after all
validation, snapshot, configuration-freeze, and training checks have passed.
No tuning, alternative-model evaluation, promotion, or deployment is performed.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import platform
import shutil
import subprocess
import sys
from collections import Counter
from datetime import date, datetime
from pathlib import Path
from typing import Any, Iterable
from zoneinfo import ZoneInfo

import joblib
import numpy as np
import pandas as pd
import scipy
import sklearn
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    accuracy_score,
    balanced_accuracy_score,
    confusion_matrix,
    f1_score,
    precision_recall_fscore_support,
    precision_score,
    recall_score,
)
from sklearn.pipeline import FeatureUnion, Pipeline

import config
from boilerplate_transformer import BOILERPLATE_PHRASES, BoilerplateStripper


ML_ROOT = Path(__file__).resolve().parent.parent
RESULTS_ROOT = ML_ROOT / "results"
EXP5_ROOT = RESULTS_ROOT / "experiment5_combined_real_synthetic"
OUTPUT_DIR = RESULTS_ROOT / "experiment6_arm_e_prospective_synthetic_test"

BASELINE_PATH = ML_ROOT / "data" / "state_land_governance_confirmed_200.csv"
CANDIDATE_PATH = ML_ROOT / "data" / "state_land_complaints_development_530.csv"
DEVELOPMENT_REVIEW_PATH = ML_ROOT / "candidate-data" / "v1" / "synthetic_family_review_pack_confirmed_20261007.csv"
PROSPECTIVE_REVIEW_PATH = ML_ROOT / "candidate-data" / "v1" / "review-assistance" / "prospective_test_family_review_corrected_20261011.csv"
PROSPECTIVE_TEMPLATE_PATH = ML_ROOT / "candidate-data" / "v1" / "review-assistance" / "prospective_test_family_review_template.csv"
PROSPECTIVE_REVIEW_MANIFEST_PATH = ML_ROOT / "candidate-data" / "v1" / "review-assistance" / "prospective_test_review_manifest.json"
EXP5_PROTOCOL_PATH = EXP5_ROOT / "experiment5_protocol.json"
EXP5_SUMMARY_PATH = EXP5_ROOT / "experiment5_summary.json"
EXP5_ARM_E_METADATA_PATH = EXP5_ROOT / "arm_e" / "run_metadata.json"
EXP5_POPULATION_MANIFEST_PATH = EXP5_ROOT / "experiment5_population_manifest.csv"

ID_COLUMN = config.ID_COLUMN
TEXT_COLUMN = config.TEXT_COLUMN
LABEL_COLUMN = config.TARGET_COLUMN
FAMILY_COLUMN = "Synthetic_Family_ID"
VARIANT_COLUMN = "Synthetic_Variant"
LABEL_ORDER = list(config.LABEL_ORDER)
RANDOM_STATE = config.RANDOM_STATE
PROBABILITY_TOLERANCE = 1e-10

BASELINE_ORIGIN = "Existing baseline"
SOURCE_ORIGIN = "New source-derived candidate"
SYNTHETIC_ORIGIN = "New synthetic scenario"
ALLOWED_DECISIONS = {"Approved", "Label_Change", "Rejected", "Needs_Discussion"}
ELIGIBLE_DECISIONS = {"Approved", "Label_Change"}
EXCLUDED_DECISIONS = {"Rejected", "Needs_Discussion"}
REQUIRED_REVIEW_FIELDS = ["Reviewer", "Review_Date", "Review_Decision", "Variant_Consistency", "Review_Notes"]


def sha256_bytes(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def canonical_json_bytes(payload: Any) -> bytes:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def canonical_hash(payload: Any) -> str:
    return sha256_bytes(canonical_json_bytes(payload))


def write_json(path: Path, payload: Any) -> None:
    def default(value: Any) -> Any:
        if isinstance(value, (np.integer,)):
            return int(value)
        if isinstance(value, (np.floating,)):
            return float(value)
        if isinstance(value, Path):
            return str(value)
        raise TypeError(f"Cannot encode {type(value).__name__}")

    path.write_text(json.dumps(payload, indent=2, default=default) + "\n", encoding="utf-8")


def read_csv_with_detected_encoding(path: Path) -> tuple[pd.DataFrame, str]:
    raw = path.read_bytes()
    try:
        raw.decode("utf-8")
        encoding = "utf-8"
    except UnicodeDecodeError:
        encoding = "cp1252"
    return pd.read_csv(path, encoding=encoding, keep_default_na=False), encoding


def _require_columns(frame: pd.DataFrame, columns: Iterable[str], source: str) -> None:
    missing = set(columns) - set(frame.columns)
    if missing:
        raise ValueError(f"{source} missing required columns: {sorted(missing)}")


def _blank(value: Any) -> bool:
    return value is None or (isinstance(value, float) and math.isnan(value)) or str(value).strip() == ""


def validate_unique_ids(frame: pd.DataFrame, source: str) -> None:
    _require_columns(frame, [ID_COLUMN], source)
    blanks = frame[ID_COLUMN].map(_blank)
    if blanks.any():
        raise ValueError(f"{source} has {int(blanks.sum())} blank Research_ID values")
    duplicates = frame[ID_COLUMN].duplicated(keep=False)
    if duplicates.any():
        raise ValueError(f"{source} has duplicate Research_ID values: {sorted(frame.loc[duplicates, ID_COLUMN].unique())[:5]}")


def parse_review_date(value: Any) -> date:
    text = str(value).strip()
    for fmt in ("%m/%d/%Y", "%Y-%m-%d", "%d/%m/%Y"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            continue
    raise ValueError(f"Unparseable Review_Date: {text!r}")


def validate_review_decisions(review: pd.DataFrame, evaluation_date: date) -> tuple[pd.DataFrame, pd.DataFrame]:
    required = [FAMILY_COLUMN, "Proposed_Label", "Approved_Label", *REQUIRED_REVIEW_FIELDS]
    _require_columns(review, required, "prospective review")
    if len(review) != 25:
        raise ValueError(f"Prospective review must contain exactly 25 families; found {len(review)}")
    if review[FAMILY_COLUMN].map(_blank).any() or review[FAMILY_COLUMN].duplicated().any():
        raise ValueError("Prospective review family IDs must be nonblank and unique")
    for field in REQUIRED_REVIEW_FIELDS:
        blanks = review[field].map(_blank)
        if blanks.any():
            raise ValueError(f"Prospective review has {int(blanks.sum())} blank {field} values")
    unknown = set(review["Review_Decision"].astype(str)) - ALLOWED_DECISIONS
    if unknown:
        raise ValueError(f"Unknown prospective Review_Decision values: {sorted(unknown)}")
    invalid_consistency = review["Variant_Consistency"].astype(str).str.strip().eq("")
    if invalid_consistency.any():
        raise ValueError("Variant_Consistency must be completed for every family")
    invalid_proposed = set(review["Proposed_Label"].astype(str)) - set(LABEL_ORDER)
    if invalid_proposed:
        raise ValueError(f"Prospective proposed labels outside taxonomy: {sorted(invalid_proposed)}")

    final_labels: list[str | None] = []
    for _, row in review.iterrows():
        family_id = str(row[FAMILY_COLUMN])
        decision = str(row["Review_Decision"])
        proposed = str(row["Proposed_Label"])
        approved = str(row["Approved_Label"]).strip()
        review_date = parse_review_date(row["Review_Date"])
        if review_date > evaluation_date:
            raise ValueError(f"Review date for {family_id} is after evaluation date: {review_date}")
        if decision == "Approved":
            if approved and approved != proposed:
                raise ValueError(f"Approved family {family_id} has Approved_Label different from Proposed_Label")
            final_labels.append(proposed)
        elif decision == "Label_Change":
            if approved not in LABEL_ORDER:
                raise ValueError(f"Label_Change family {family_id} lacks a valid Approved_Label")
            if _blank(row["Review_Notes"]):
                raise ValueError(f"Label_Change family {family_id} requires Review_Notes")
            final_labels.append(approved)
        else:
            final_labels.append(None)
    validated = review.copy()
    validated["Final_Label"] = final_labels
    eligible = validated[validated["Review_Decision"].isin(ELIGIBLE_DECISIONS)].copy()
    excluded = validated[validated["Review_Decision"].isin(EXCLUDED_DECISIONS)].copy()
    if len(eligible) + len(excluded) != 25:
        raise AssertionError("Prospective eligibility partition is incomplete")
    represented = set(eligible["Final_Label"].dropna().astype(str))
    if represented != set(LABEL_ORDER):
        missing = sorted(set(LABEL_ORDER) - represented)
        raise ValueError(f"PROSPECTIVE TEST POPULATION INADEQUATE: eligible families do not represent all four classes; missing {missing}")
    return eligible, excluded


def _variant_key(value: Any) -> tuple[int, str]:
    text = str(value).strip()
    digits = "".join(character for character in text if character.isdigit())
    return (int(digits) if digits else 10**9, text)


def validate_prospective_records(
    review: pd.DataFrame,
    template: pd.DataFrame,
    candidate: pd.DataFrame,
    development_family_ids: set[str],
    experiment5_oof_ids: set[str],
) -> tuple[pd.DataFrame, pd.DataFrame]:
    _require_columns(template, [FAMILY_COLUMN, "Research_ID_V1", "Research_ID_V2", "Research_ID_V3", "Canonical_English_Text_V1", "Canonical_English_Text_V2", "Canonical_English_Text_V3", "Proposed_Label"], "prospective template")
    _require_columns(candidate, [ID_COLUMN, TEXT_COLUMN, LABEL_COLUMN, "Record_Origin", FAMILY_COLUMN, VARIANT_COLUMN], "candidate dataset")
    if set(review[FAMILY_COLUMN].astype(str)) != set(template[FAMILY_COLUMN].astype(str)):
        raise ValueError("Corrected review family IDs do not exactly match the prospective template")
    template_labels = template.set_index(FAMILY_COLUMN)["Proposed_Label"].astype(str).sort_index()
    review_labels = review.set_index(FAMILY_COLUMN)["Proposed_Label"].astype(str).sort_index()
    if not template_labels.equals(review_labels):
        raise ValueError("Corrected review proposed labels differ from the frozen prospective template")
    family_ids = set(review[FAMILY_COLUMN].astype(str))
    if family_ids & development_family_ids:
        raise ValueError("Prospective and Experiment 5 development synthetic families overlap")
    prospective = candidate[
        candidate["Record_Origin"].eq(SYNTHETIC_ORIGIN)
        & candidate[FAMILY_COLUMN].fillna("").astype(str).isin(family_ids)
    ].copy()
    if len(prospective) != 75 or prospective[ID_COLUMN].nunique() != 75:
        raise ValueError("Prospective source population must contain exactly 75 unique records")
    if (prospective.groupby(FAMILY_COLUMN).size() != 3).any() or (prospective.groupby(FAMILY_COLUMN)[VARIANT_COLUMN].nunique() != 3).any():
        raise ValueError("Each prospective family must contain exactly three unique variants")
    if (prospective.groupby(FAMILY_COLUMN)[LABEL_COLUMN].nunique() != 1).any():
        raise ValueError("A prospective family has multiple source proposed labels")
    if set(prospective[ID_COLUMN].astype(str)) & experiment5_oof_ids:
        raise ValueError("Prospective record IDs entered an Experiment 5 OOF arm")
    if candidate[candidate["Record_Origin"].eq(SOURCE_ORIGIN)][FAMILY_COLUMN].fillna("").astype(str).isin(family_ids).any():
        raise ValueError("A source-derived candidate overlaps the prospective family population")
    validate_unique_ids(prospective, "prospective source population")
    for _, template_row in template.iterrows():
        family_id = str(template_row[FAMILY_COLUMN])
        family = prospective[prospective[FAMILY_COLUMN].astype(str).eq(family_id)].copy()
        family["_variant_key"] = family[VARIANT_COLUMN].map(_variant_key)
        family = family.sort_values("_variant_key")
        expected_ids = [str(template_row[f"Research_ID_V{variant}"]) for variant in (1, 2, 3)]
        expected_texts = [str(template_row[f"Canonical_English_Text_V{variant}"]) for variant in (1, 2, 3)]
        if family[ID_COLUMN].astype(str).tolist() != expected_ids:
            raise ValueError(f"Prospective Research_ID/family/variant mismatch for {family_id}")
        if family[TEXT_COLUMN].astype(str).tolist() != expected_texts:
            raise ValueError(f"Prospective exact-text mismatch for {family_id}")
    final_label_map = review.set_index(FAMILY_COLUMN)["Final_Label"].to_dict()
    prospective["Final_Label"] = prospective[FAMILY_COLUMN].map(final_label_map)
    eligible_records = prospective[prospective["Final_Label"].notna()].copy()
    excluded_records = prospective[prospective["Final_Label"].isna()].copy()
    return eligible_records, excluded_records


def get_experiment5_development_families(development_review: pd.DataFrame) -> set[str]:
    _require_columns(development_review, [FAMILY_COLUMN, "Review_Decision"], "Experiment 5 development review")
    eligible = development_review["Review_Decision"].fillna("").astype(str).isin({"Approved", "Label_Change"})
    families = set(development_review.loc[eligible, FAMILY_COLUMN].astype(str))
    if len(families) != 75:
        raise ValueError(f"Expected 75 Experiment 5 development families; found {len(families)}")
    return families


def get_experiment5_oof_ids() -> set[str]:
    all_ids: set[str] = set()
    for arm in ("arm_a", "arm_b", "arm_c", "arm_d", "arm_e"):
        path = EXP5_ROOT / arm / "oof_predictions_synthetic.csv"
        frame = pd.read_csv(path)
        _require_columns(frame, [ID_COLUMN], f"Experiment 5 {arm} synthetic OOF")
        all_ids.update(frame[ID_COLUMN].astype(str))
    return all_ids


def _jsonable_config(value: Any) -> Any:
    if isinstance(value, tuple):
        return [_jsonable_config(item) for item in value]
    if isinstance(value, dict):
        return {key: _jsonable_config(item) for key, item in value.items()}
    return value


def reconstruct_frozen_arm_e_configuration(protocol: dict[str, Any], metadata: dict[str, Any]) -> dict[str, Any]:
    if protocol.get("experiment") != 5 or "arm_e" not in protocol.get("arms", []):
        raise ValueError("Experiment 5 protocol does not freeze Arm E")
    if metadata.get("arm") != "arm_e" or metadata.get("pipeline") != "word + character TF-IDF":
        raise ValueError("Experiment 5 Arm E metadata pipeline identity mismatch")
    if protocol.get("class_order") != LABEL_ORDER or metadata.get("probability_class_order") != LABEL_ORDER:
        raise ValueError("Saved Experiment 5 class order differs from the configured taxonomy")
    if protocol.get("random_seed") != RANDOM_STATE or metadata.get("random_seed") != RANDOM_STATE:
        raise ValueError("Saved Experiment 5 random seed differs from frozen configuration")
    if metadata.get("real_training_weight") != 1.0 or metadata.get("synthetic_training") != "all variants, each family total weight 1":
        raise ValueError("Saved Experiment 5 sample-weight policy mismatch")
    expected_char = {"analyzer": "char_wb", "ngram_range": [3, 5], "lowercase": True, "sublinear_tf": True, "min_df": 1}
    if metadata.get("character_tfidf") != expected_char:
        raise ValueError("Saved Experiment 5 character TF-IDF parameters differ from the frozen Arm E configuration")
    return {
        "architecture": "Pipeline(BoilerplateStripper, FeatureUnion(word, character), LogisticRegression)",
        "boilerplate_stripper": {
            "class": "boilerplate_transformer.BoilerplateStripper",
            "parameters": {},
            "exact_phrases": list(BOILERPLATE_PHRASES),
        },
        "feature_union": {
            "transformers": ["word", "character"],
            "word_tfidf": _jsonable_config(config.TFIDF_CONFIG),
            "character_tfidf": expected_char,
        },
        "logistic_regression": _jsonable_config(config.LR_CONFIG),
        "class_order": LABEL_ORDER,
        "sample_weight_policy": {"real_record": 1.0, "synthetic_variant": 1.0 / 3.0, "synthetic_family_total": 1.0},
        "random_seed": RANDOM_STATE,
        "evidence": {
            "protocol": str(EXP5_PROTOCOL_PATH.relative_to(ML_ROOT)).replace("\\", "/"),
            "arm_e_run_metadata": str(EXP5_ARM_E_METADATA_PATH.relative_to(ML_ROOT)).replace("\\", "/"),
            "word_and_lr_parameters_source": "src/config.py used by the saved Experiment 5 implementation",
        },
    }


def build_frozen_arm_e_pipeline(frozen: dict[str, Any]) -> Pipeline:
    word_config = dict(frozen["feature_union"]["word_tfidf"])
    word_config["ngram_range"] = tuple(word_config["ngram_range"])
    char_config = dict(frozen["feature_union"]["character_tfidf"])
    char_config["ngram_range"] = tuple(char_config["ngram_range"])
    lr_config = dict(frozen["logistic_regression"])
    return Pipeline([
        ("boilerplate", BoilerplateStripper()),
        ("features", FeatureUnion([
            ("word", TfidfVectorizer(**word_config)),
            ("character", TfidfVectorizer(**char_config)),
        ])),
        ("clf", LogisticRegression(**lr_config)),
    ])


def family_balanced_weights(training: pd.DataFrame) -> np.ndarray:
    weights = np.ones(len(training), dtype=float)
    synthetic_mask = training["population_origin"].eq("development_synthetic").to_numpy()
    weights[synthetic_mask] = 1.0 / 3.0
    return weights


def fit_pipeline_once(pipeline: Any, texts: list[str], labels: list[str], weights: np.ndarray) -> Any:
    return pipeline.fit(texts, labels, clf__sample_weight=weights)


def build_training_population(
    baseline: pd.DataFrame,
    candidate: pd.DataFrame,
    development_review: pd.DataFrame,
    prospective_family_ids: set[str],
) -> pd.DataFrame:
    required = [ID_COLUMN, TEXT_COLUMN, LABEL_COLUMN]
    _require_columns(baseline, required, "real baseline")
    _require_columns(candidate, [*required, "Record_Origin", FAMILY_COLUMN, VARIANT_COLUMN], "candidate dataset")
    validate_unique_ids(baseline, "real baseline")
    candidate_baseline = candidate[candidate["Record_Origin"].eq(BASELINE_ORIGIN)].copy()
    compare = [ID_COLUMN, TEXT_COLUMN, LABEL_COLUMN, config.GROUP_COLUMN]
    if not baseline[compare].sort_values(ID_COLUMN).reset_index(drop=True).equals(candidate_baseline[compare].sort_values(ID_COLUMN).reset_index(drop=True)):
        raise ValueError("Candidate baseline does not exactly match the original 200-row baseline")
    development_family_ids = get_experiment5_development_families(development_review)
    if development_family_ids & prospective_family_ids:
        raise ValueError("Prospective families overlap the development training families")
    synthetic = candidate[
        candidate["Record_Origin"].eq(SYNTHETIC_ORIGIN)
        & candidate[FAMILY_COLUMN].fillna("").astype(str).isin(development_family_ids)
    ].copy()
    if len(baseline) != 200 or len(synthetic) != 225 or synthetic[FAMILY_COLUMN].nunique() != 75:
        raise ValueError(f"Final training population mismatch: real={len(baseline)}, synthetic_records={len(synthetic)}, synthetic_families={synthetic[FAMILY_COLUMN].nunique()}")
    if (synthetic.groupby(FAMILY_COLUMN).size() != 3).any():
        raise ValueError("A development synthetic family does not have exactly three variants")
    final_label_map = development_review.set_index(FAMILY_COLUMN).apply(
        lambda row: str(row["Approved_Label"]).strip() if str(row["Review_Decision"]) == "Label_Change" else str(row["Proposed_Label"]), axis=1
    ).to_dict()
    synthetic["Training_Label"] = synthetic[FAMILY_COLUMN].map(final_label_map)
    real = baseline.copy()
    real["Training_Label"] = real[LABEL_COLUMN]
    real["population_origin"] = "real_baseline"
    real[FAMILY_COLUMN] = ""
    real[VARIANT_COLUMN] = ""
    synthetic["population_origin"] = "development_synthetic"
    training = pd.concat([real, synthetic], ignore_index=True)
    validate_unique_ids(training, "final training population")
    if set(training[ID_COLUMN].astype(str)) & set(candidate[candidate[FAMILY_COLUMN].fillna("").astype(str).isin(prospective_family_ids)][ID_COLUMN].astype(str)):
        raise ValueError("Prospective records entered the final training population")
    training["sample_weight"] = family_balanced_weights(training)
    family_weight = training[training["population_origin"].eq("development_synthetic")].groupby(FAMILY_COLUMN)["sample_weight"].sum()
    if not np.allclose(family_weight.to_numpy(), 1.0, atol=1e-12, rtol=0.0):
        raise ValueError("Development synthetic family weights do not total one")
    return training


def probability_column(label: str) -> str:
    return f"prob_{label.replace('/', '_').replace(' ', '_')}"


PROBABILITY_COLUMNS = [probability_column(label) for label in LABEL_ORDER]


def align_probabilities(classes: Iterable[str], raw: np.ndarray) -> np.ndarray:
    classes = [str(item) for item in classes]
    if set(classes) != set(LABEL_ORDER):
        raise ValueError(f"Fitted classifier class set differs from taxonomy: {classes}")
    indices = [classes.index(label) for label in LABEL_ORDER]
    return np.asarray(raw, dtype=float)[:, indices]


def validate_probabilities(probabilities: np.ndarray, predicted: Iterable[str]) -> dict[str, Any]:
    matrix = np.asarray(probabilities, dtype=float)
    if matrix.ndim != 2 or matrix.shape[1] != len(LABEL_ORDER):
        raise ValueError(f"Expected four probability columns; got {matrix.shape}")
    if not np.isfinite(matrix).all():
        raise ValueError("Probabilities contain non-finite values")
    if ((matrix < 0.0) | (matrix > 1.0)).any():
        raise ValueError("Probabilities fall outside [0, 1]")
    row_sums = matrix.sum(axis=1)
    if not np.allclose(row_sums, 1.0, atol=PROBABILITY_TOLERANCE, rtol=0.0):
        raise ValueError("Probability rows do not sum to one within tolerance")
    expected = np.asarray(LABEL_ORDER, dtype=object)[matrix.argmax(axis=1)]
    if not np.array_equal(expected, np.asarray(list(predicted), dtype=object)):
        raise ValueError("Predicted labels do not match probability argmax")
    return {
        "finite": True,
        "inside_unit_interval": True,
        "row_sums_within_tolerance": True,
        "argmax_agreement": True,
        "row_sum_tolerance": PROBABILITY_TOLERANCE,
        "maximum_row_sum_error": float(np.max(np.abs(row_sums - 1.0))),
    }


def wilson_interval(correct: int, support: int, z: float = 1.959963984540054) -> tuple[float, float]:
    if support <= 0:
        raise ValueError("Wilson interval requires positive support")
    proportion = correct / support
    denominator = 1.0 + z * z / support
    centre = (proportion + z * z / (2.0 * support)) / denominator
    margin = z * math.sqrt(proportion * (1.0 - proportion) / support + z * z / (4.0 * support * support)) / denominator
    return centre - margin, centre + margin


def calculate_record_metrics(predictions: pd.DataFrame) -> tuple[dict[str, Any], pd.DataFrame, pd.DataFrame]:
    true = predictions["True_Label"].astype(str).tolist()
    predicted = predictions["Predicted_Label"].astype(str).tolist()
    precision, recall, f1, support = precision_recall_fscore_support(true, predicted, labels=LABEL_ORDER, zero_division=0)
    cm = confusion_matrix(true, predicted, labels=LABEL_ORDER)
    per_class = pd.DataFrame({"label": LABEL_ORDER, "precision": precision, "recall": recall, "f1": f1, "support": support.astype(int)})
    correct = int((predictions["True_Label"] == predictions["Predicted_Label"]).sum())
    lower, upper = wilson_interval(correct, len(predictions))
    metrics = {
        "evaluation_type": "synthetic prospective evaluation",
        "support": int(len(predictions)),
        "correct": correct,
        "incorrect": int(len(predictions) - correct),
        "accuracy": float(accuracy_score(true, predicted)),
        "balanced_accuracy": float(balanced_accuracy_score(true, predicted)),
        "macro_precision": float(precision_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0)),
        "macro_recall": float(recall_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0)),
        "macro_f1": float(f1_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0)),
        "weighted_f1": float(f1_score(true, predicted, labels=LABEL_ORDER, average="weighted", zero_division=0)),
        "wilson_95_accuracy_interval": {"lower": lower, "upper": upper},
    }
    cm_frame = pd.DataFrame(cm, index=LABEL_ORDER, columns=LABEL_ORDER)
    cm_frame.index.name = "true_label"
    return metrics, per_class, cm_frame


def calculate_family_predictions(predictions: pd.DataFrame) -> tuple[pd.DataFrame, dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for family_id, family in predictions.groupby(FAMILY_COLUMN, sort=True):
        if len(family) != 3 or family["True_Label"].nunique() != 1:
            raise ValueError(f"Eligible prospective family {family_id} is not three same-label variants")
        counts = Counter(family["Predicted_Label"].astype(str))
        max_votes = max(counts.values())
        tied = [label for label in LABEL_ORDER if counts[label] == max_votes]
        if len(tied) == 1:
            majority = tied[0]
            tie_break = "not_required"
        else:
            sums = {label: float(family[probability_column(label)].sum()) for label in tied}
            max_sum = max(sums.values())
            probability_tied = [label for label in tied if math.isclose(sums[label], max_sum, rel_tol=0.0, abs_tol=1e-15)]
            majority = min(probability_tied, key=LABEL_ORDER.index)
            tie_break = "summed_probability_then_frozen_class_order"
        correct_variants = int((family["True_Label"] == family["Predicted_Label"]).sum())
        rows.append({
            FAMILY_COLUMN: family_id,
            "True_Label": family["True_Label"].iloc[0],
            "Family_Majority_Prediction": majority,
            "Family_Majority_Correct": majority == family["True_Label"].iloc[0],
            "Correct_Variants": correct_variants,
            "Variant_Prediction_Disagreement": family["Predicted_Label"].nunique() > 1,
            "Majority_Tie": len(tied) > 1,
            "Tied_Labels": ";".join(tied) if len(tied) > 1 else "",
            "Tie_Break": tie_break,
            "Variant_Predictions": ";".join(family.sort_values(VARIANT_COLUMN)["Predicted_Label"].astype(str)),
        })
    family_frame = pd.DataFrame(rows)
    distribution = family_frame["Correct_Variants"].value_counts().reindex(range(4), fill_value=0).sort_index()
    metrics = {
        "eligible_family_count": int(len(family_frame)),
        "family_majority_accuracy": float(accuracy_score(family_frame["True_Label"], family_frame["Family_Majority_Prediction"])),
        "family_majority_macro_f1": float(f1_score(family_frame["True_Label"], family_frame["Family_Majority_Prediction"], labels=LABEL_ORDER, average="macro", zero_division=0)),
        "families_all_three_variants_correct": int((family_frame["Correct_Variants"] == 3).sum()),
        "correct_variants_distribution": {str(index): int(value) for index, value in distribution.items()},
        "families_with_variant_prediction_disagreement": int(family_frame["Variant_Prediction_Disagreement"].sum()),
        "families_with_majority_ties": int(family_frame["Majority_Tie"].sum()),
    }
    return family_frame, metrics


def promotion_decision(
    metrics: dict[str, Any],
    per_class: pd.DataFrame,
    probability_validation: dict[str, Any],
    integrity_passed: bool,
) -> dict[str, Any]:
    represented = per_class[per_class["support"] > 0]
    checks = {
        "accuracy_strictly_greater_than_0_80": metrics["accuracy"] > 0.80,
        "macro_f1_strictly_greater_than_0_80": metrics["macro_f1"] > 0.80,
        "no_represented_class_zero_recall": bool((represented["recall"] > 0.0).all()),
        "probability_validation_passed": all(probability_validation[key] for key in ("finite", "inside_unit_interval", "row_sums_within_tolerance", "argmax_agreement")),
        "integrity_and_leakage_checks_passed": integrity_passed,
        "labelled_synthetic_prospective_evaluation": metrics.get("evaluation_type") == "synthetic prospective evaluation",
    }
    passed = all(checks.values())
    verdict = (
        "ARM E PASSED PROSPECTIVE SYNTHETIC EVALUATION — PROMOTION REVIEW REQUIRED"
        if passed
        else "ARM E FAILED PROSPECTIVE SYNTHETIC EVALUATION — DO NOT PROMOTE"
    )
    return {"checks": checks, "all_criteria_passed": passed, "verdict": verdict, "automatic_deployment_authorized": False}


def dependency_versions() -> dict[str, str]:
    return {
        "python": platform.python_version(),
        "numpy": np.__version__,
        "pandas": pd.__version__,
        "scikit_learn": sklearn.__version__,
        "scipy": scipy.__version__,
        "joblib": joblib.__version__,
    }


def protected_inventory() -> dict[str, str]:
    roots = [ML_ROOT / "models" / "v1", ML_ROOT / "anomaly_detection" / "bundles", ML_ROOT / "anomaly_detection" / "runs", ML_ROOT / "anomaly_detection" / "evaluations"]
    files: set[Path] = {BASELINE_PATH, CANDIDATE_PATH, DEVELOPMENT_REVIEW_PATH, PROSPECTIVE_REVIEW_PATH}
    for root in roots:
        if root.exists():
            files.update(path for path in root.rglob("*") if path.is_file())
    if RESULTS_ROOT.exists():
        files.update(path for path in RESULTS_ROOT.rglob("*") if path.is_file() and OUTPUT_DIR not in path.parents)
    return {str(path.relative_to(ML_ROOT)).replace("\\", "/"): sha256_file(path) for path in sorted(files)}


def inventory_digest(inventory: dict[str, str]) -> str:
    return canonical_hash(inventory)


def input_hashes() -> dict[str, dict[str, str]]:
    paths = {
        "real_baseline": BASELINE_PATH,
        "candidate_dataset": CANDIDATE_PATH,
        "experiment5_development_review": DEVELOPMENT_REVIEW_PATH,
        "prospective_corrected_review": PROSPECTIVE_REVIEW_PATH,
        "prospective_review_manifest": PROSPECTIVE_REVIEW_MANIFEST_PATH,
        "prospective_review_template": PROSPECTIVE_TEMPLATE_PATH,
        "experiment5_protocol": EXP5_PROTOCOL_PATH,
        "experiment5_summary": EXP5_SUMMARY_PATH,
        "experiment5_arm_e_metadata": EXP5_ARM_E_METADATA_PATH,
        "experiment5_population_manifest": EXP5_POPULATION_MANIFEST_PATH,
    }
    missing = [str(path) for path in paths.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Required Experiment 6 inputs are missing: {missing}")
    return {name: {"path": str(path.relative_to(ML_ROOT)).replace("\\", "/"), "sha256": sha256_file(path)} for name, path in paths.items()}


def ensure_new_output_directory(path: Path) -> None:
    if path.exists():
        raise FileExistsError(f"Experiment 6 output path already exists; refusing a second run or overwrite: {path}")
    path.mkdir(parents=True, exist_ok=False)


def run_preflight() -> dict[str, Any]:
    hashes = input_hashes()
    review, review_encoding = read_csv_with_detected_encoding(PROSPECTIVE_REVIEW_PATH)
    template, template_encoding = read_csv_with_detected_encoding(PROSPECTIVE_TEMPLATE_PATH)
    candidate = pd.read_csv(CANDIDATE_PATH)
    baseline = pd.read_csv(BASELINE_PATH)
    development_review = pd.read_csv(DEVELOPMENT_REVIEW_PATH)
    protocol = json.loads(EXP5_PROTOCOL_PATH.read_text(encoding="utf-8"))
    arm_e_metadata = json.loads(EXP5_ARM_E_METADATA_PATH.read_text(encoding="utf-8"))
    evaluation_date = datetime.now(ZoneInfo("Asia/Colombo")).date()
    eligible_families, excluded_families = validate_review_decisions(review, evaluation_date)
    development_families = get_experiment5_development_families(development_review)
    eligible_records, excluded_records = validate_prospective_records(
        pd.concat([eligible_families, excluded_families]).sort_index(),
        template,
        candidate,
        development_families,
        get_experiment5_oof_ids(),
    )
    training = build_training_population(baseline, candidate, development_review, set(review[FAMILY_COLUMN].astype(str)))
    if set(training[TEXT_COLUMN].astype(str)) & set(eligible_records[TEXT_COLUMN].astype(str)):
        raise ValueError("Exact text overlap exists between final training and eligible prospective populations")
    frozen = reconstruct_frozen_arm_e_configuration(protocol, arm_e_metadata)
    if set(eligible_records["Final_Label"].astype(str)) != set(LABEL_ORDER):
        raise ValueError("PROSPECTIVE TEST POPULATION INADEQUATE: eligible records lack four-class coverage")
    forbidden_model_columns = [column for column in review.columns if column == "Predicted_Label" or column.startswith("prob_") or column in {"Prediction", "Model_Output"}]
    if forbidden_model_columns:
        raise ValueError(f"Prospective review contains forbidden model-output columns: {forbidden_model_columns}")
    return {
        "hashes": hashes,
        "review": review,
        "review_encoding": review_encoding,
        "template_encoding": template_encoding,
        "eligible_families": eligible_families,
        "excluded_families": excluded_families,
        "eligible_records": eligible_records,
        "excluded_records": excluded_records,
        "training": training,
        "frozen_configuration": frozen,
        "configuration_sha256": canonical_hash(frozen),
        "dependency_versions": dependency_versions(),
        "evaluation_date": evaluation_date.isoformat(),
        "summary": {
            "reviewed_families": int(len(review)),
            "prospective_records_before_filtering": 75,
            "eligible_families": int(len(eligible_families)),
            "eligible_records": int(len(eligible_records)),
            "excluded_families": int(len(excluded_families)),
            "excluded_records": int(len(excluded_records)),
            "eligible_class_distribution_records": eligible_records["Final_Label"].value_counts().reindex(LABEL_ORDER, fill_value=0).astype(int).to_dict(),
            "training_real_records": int(training["population_origin"].eq("real_baseline").sum()),
            "training_synthetic_records": int(training["population_origin"].eq("development_synthetic").sum()),
            "training_synthetic_families": int(training.loc[training["population_origin"].eq("development_synthetic"), FAMILY_COLUMN].nunique()),
        },
    }


def _git_info() -> dict[str, Any]:
    try:
        return {
            "branch": subprocess.check_output(["git", "branch", "--show-current"], cwd=ML_ROOT, text=True).strip(),
            "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ML_ROOT, text=True).strip(),
        }
    except Exception as exc:
        return {"error": str(exc)}


def _report(
    preflight: dict[str, Any],
    metrics: dict[str, Any],
    family_metrics: dict[str, Any],
    per_class: pd.DataFrame,
    decision: dict[str, Any],
    integrity: dict[str, Any],
) -> str:
    eligible_ids = ", ".join(preflight["eligible_families"][FAMILY_COLUMN].astype(str))
    excluded_ids = ", ".join(preflight["excluded_families"][FAMILY_COLUMN].astype(str)) or "None"
    lines = [
        "# Experiment 6 — Frozen Arm E Prospective Synthetic Evaluation",
        "",
        "> Synthetic prospective evaluation only. These results are not real-world accuracy. The candidate was not deployed.",
        "",
        "## Review validation",
        "",
        f"- Reviewed families/records before filtering: 25 / 75",
        f"- Eligible families/records: {len(preflight['eligible_families'])} / {len(preflight['eligible_records'])}",
        f"- Excluded families/records: {len(preflight['excluded_families'])} / {len(preflight['excluded_records'])}",
        f"- Eligible: {eligible_ids}",
        f"- Excluded: {excluded_ids}",
        "",
        "## Final training population",
        "",
        "200 real baseline records plus 225 records from 75 approved development synthetic families. Real weights were 1.0 and synthetic variant weights were 1/3. Source-derived and prospective records were excluded.",
        "",
        "## Frozen configuration",
        "",
        f"- Canonical configuration SHA-256: `{preflight['configuration_sha256']}`",
        "- BoilerplateStripper → FeatureUnion(established word TF-IDF, char_wb 3–5 TF-IDF) → LogisticRegression",
        "- Random seed: 42; frozen four-class order preserved.",
        "",
        "## Record-level results",
        "",
        f"- Support: {metrics['support']}; correct: {metrics['correct']}; incorrect: {metrics['incorrect']}",
        f"- Accuracy: {metrics['accuracy']:.9f}",
        f"- Balanced accuracy: {metrics['balanced_accuracy']:.9f}",
        f"- Macro precision / recall / F1: {metrics['macro_precision']:.9f} / {metrics['macro_recall']:.9f} / {metrics['macro_f1']:.9f}",
        f"- Weighted F1: {metrics['weighted_f1']:.9f}",
        f"- Wilson 95% accuracy interval: [{metrics['wilson_95_accuracy_interval']['lower']:.9f}, {metrics['wilson_95_accuracy_interval']['upper']:.9f}]",
        "",
        "## Family-level results",
        "",
        f"- Eligible families: {family_metrics['eligible_family_count']}",
        f"- Majority accuracy / macro-F1: {family_metrics['family_majority_accuracy']:.9f} / {family_metrics['family_majority_macro_f1']:.9f}",
        f"- All three variants correct: {family_metrics['families_all_three_variants_correct']}",
        f"- 0/1/2/3 correct distribution: {family_metrics['correct_variants_distribution']}",
        f"- Variant disagreements: {family_metrics['families_with_variant_prediction_disagreement']}; majority ties: {family_metrics['families_with_majority_ties']}",
        "",
        "## Per-class results",
        "",
    ]
    for _, row in per_class.iterrows():
        lines.append(f"- {row['label']}: support {int(row['support'])}, precision {row['precision']:.9f}, recall {row['recall']:.9f}, F1 {row['f1']:.9f}")
    lines.extend([
        "",
        "## Integrity, decision, and limitations",
        "",
        f"- Protected input preservation: {integrity['protected_inputs_unchanged']}",
        "- No training/test ID, family, or exact-text overlap; no additional model selection or tuning was performed.",
        "- The candidate is unpromoted and stored only inside this Experiment 6 directory.",
        "- Both development and prospective synthetic data share a common generator origin, so performance may reflect generator regularities and must not be treated as real-world accuracy.",
        "",
        f"## Verdict\n\n**{decision['verdict']}**",
        "",
    ])
    return "\n".join(lines)


def run_experiment6() -> dict[str, Any]:
    if OUTPUT_DIR.exists():
        raise FileExistsError(f"Experiment 6 output already exists; one-time evaluation will not be rerun: {OUTPUT_DIR}")
    protected_before = protected_inventory()
    preflight = run_preflight()

    # Freeze the human review before any pipeline construction, model fit, or inference.
    ensure_new_output_directory(OUTPUT_DIR)
    snapshot_path = OUTPUT_DIR / "frozen_review_snapshot.csv"
    shutil.copyfile(PROSPECTIVE_REVIEW_PATH, snapshot_path)
    snapshot_hash = sha256_file(snapshot_path)
    if snapshot_hash != preflight["hashes"]["prospective_corrected_review"]["sha256"]:
        raise RuntimeError("Frozen review snapshot hash differs from the validated review input")
    freeze_timestamp = datetime.now(ZoneInfo("Asia/Colombo")).isoformat(timespec="seconds")

    eligible_families = preflight["eligible_families"]
    excluded_families = preflight["excluded_families"]
    eligible_records = preflight["eligible_records"].copy()
    excluded_records = preflight["excluded_records"].copy()
    training = preflight["training"].copy()

    prospective_manifest = pd.concat([
        eligible_records.assign(eligibility="eligible", exclusion_reason=""),
        excluded_records.assign(eligibility="excluded", exclusion_reason="Rejected or Needs_Discussion human decision"),
    ], ignore_index=True)[[ID_COLUMN, FAMILY_COLUMN, VARIANT_COLUMN, "Final_Label", "eligibility", "exclusion_reason"]]
    prospective_manifest.to_csv(OUTPUT_DIR / "prospective_population_manifest.csv", index=False)
    training[[ID_COLUMN, "population_origin", FAMILY_COLUMN, VARIANT_COLUMN, "Training_Label", "sample_weight"]].to_csv(OUTPUT_DIR / "candidate_training_manifest.csv", index=False)

    candidate_dir = OUTPUT_DIR / "candidate"
    candidate_dir.mkdir()
    pipeline = build_frozen_arm_e_pipeline(preflight["frozen_configuration"])
    fit_pipeline_once(
        pipeline,
        training[TEXT_COLUMN].astype(str).tolist(),
        training["Training_Label"].astype(str).tolist(),
        training["sample_weight"].to_numpy(dtype=float),
    )
    candidate_path = candidate_dir / "arm_e_candidate.joblib"
    joblib.dump(pipeline, candidate_path)
    candidate_hash = sha256_file(candidate_path)
    (candidate_dir / "candidate.sha256").write_text(f"{candidate_hash}  arm_e_candidate.joblib\n", encoding="utf-8")
    candidate_metadata = {
        "status": "unpromoted candidate; not deployed",
        "evaluation_type": "synthetic prospective evaluation",
        "configuration_sha256": preflight["configuration_sha256"],
        "model_sha256": candidate_hash,
        "training_population": {"real_records": 200, "development_synthetic_records": 225, "development_synthetic_families": 75},
        "sample_weight_policy": preflight["frozen_configuration"]["sample_weight_policy"],
        "dependency_versions": preflight["dependency_versions"],
        "fit_count": 1,
        "prospective_data_used_in_fitting": False,
    }
    write_json(candidate_dir / "candidate_metadata.json", candidate_metadata)

    # The sole prospective scoring call. Predictions derive from this one probability matrix.
    raw_probabilities = pipeline.predict_proba(eligible_records[TEXT_COLUMN].astype(str).tolist())
    probabilities = align_probabilities(pipeline.named_steps["clf"].classes_, raw_probabilities)
    predicted = np.asarray(LABEL_ORDER, dtype=object)[probabilities.argmax(axis=1)]
    probability_validation = validate_probabilities(probabilities, predicted)
    predictions = pd.DataFrame({
        ID_COLUMN: eligible_records[ID_COLUMN].astype(str).to_numpy(),
        FAMILY_COLUMN: eligible_records[FAMILY_COLUMN].astype(str).to_numpy(),
        VARIANT_COLUMN: eligible_records[VARIANT_COLUMN].to_numpy(),
        "True_Label": eligible_records["Final_Label"].astype(str).to_numpy(),
        "Predicted_Label": predicted,
        "evaluation_type": "synthetic prospective evaluation",
    })
    for index, column in enumerate(PROBABILITY_COLUMNS):
        predictions[column] = probabilities[:, index]
    predictions.to_csv(OUTPUT_DIR / "predictions.csv", index=False)

    # Aggregate metrics are intentionally calculated only after predictions are saved.
    metrics, per_class, cm = calculate_record_metrics(predictions)
    metrics["probability_validation"] = probability_validation
    family_predictions, family_metrics = calculate_family_predictions(predictions)
    metrics["family_metrics"] = family_metrics
    per_class.to_csv(OUTPUT_DIR / "per_class_metrics.csv", index=False)
    cm.to_csv(OUTPUT_DIR / "confusion_matrix.csv")
    family_predictions.to_csv(OUTPUT_DIR / "family_predictions.csv", index=False)
    write_json(OUTPUT_DIR / "metrics.json", metrics)

    protected_after = protected_inventory()
    changed = sorted(
        set(protected_before) ^ set(protected_after)
        | {path for path in set(protected_before) & set(protected_after) if protected_before[path] != protected_after[path]}
    )
    if changed:
        raise RuntimeError(f"Protected inputs changed during Experiment 6: {changed[:10]}")
    integrity = {
        "status": "PASS",
        "input_hashes": preflight["hashes"],
        "frozen_review_snapshot_sha256": snapshot_hash,
        "canonical_configuration_sha256": preflight["configuration_sha256"],
        "candidate_model_sha256": candidate_hash,
        "protected_inventory_before_sha256": inventory_digest(protected_before),
        "protected_inventory_after_sha256": inventory_digest(protected_after),
        "protected_file_count": len(protected_before),
        "protected_inputs_unchanged": True,
        "changed_protected_files": [],
        "leakage_checks": {
            "training_test_id_overlap": 0,
            "training_test_family_overlap": 0,
            "training_test_exact_text_overlap": 0,
            "source_derived_training_records": 0,
            "prospective_records_in_training": 0,
            "prospective_records_in_experiment5_oof": 0,
        },
    }
    write_json(OUTPUT_DIR / "integrity_report.json", integrity)
    decision = promotion_decision(metrics, per_class, probability_validation, integrity_passed=True)
    write_json(OUTPUT_DIR / "promotion_decision.json", decision)

    evaluation_metadata = {
        "experiment": 6,
        "evaluation_type": "synthetic prospective evaluation",
        "not_real_world_accuracy": True,
        "candidate_not_deployed": True,
        "review_freeze_timestamp": freeze_timestamp,
        "review_frozen_before_model_fit_and_inference": True,
        "frozen_review_snapshot_sha256": snapshot_hash,
        "configuration": preflight["frozen_configuration"],
        "configuration_sha256": preflight["configuration_sha256"],
        "candidate_model_sha256": candidate_hash,
        "input_hashes": preflight["hashes"],
        "dependency_versions": preflight["dependency_versions"],
        "training_population": preflight["summary"],
        "eligible_family_ids": eligible_families[FAMILY_COLUMN].astype(str).tolist(),
        "excluded_families": excluded_families[[FAMILY_COLUMN, "Review_Decision"]].to_dict(orient="records"),
        "no_additional_model_selection": True,
        "no_hyperparameter_tuning": True,
        "prospective_scoring_count": 1,
        "source_derived_records_excluded": True,
        "common_synthetic_generator_limitation": "Development and prospective synthetic families share a common generator origin; results may reflect generator regularities.",
        "git": _git_info(),
    }
    write_json(OUTPUT_DIR / "evaluation_metadata.json", evaluation_metadata)
    (OUTPUT_DIR / "experiment6_report.md").write_text(
        _report(preflight, metrics, family_metrics, per_class, decision, integrity), encoding="utf-8"
    )
    return {"preflight": preflight["summary"], "metrics": metrics, "family_metrics": family_metrics, "decision": decision, "integrity": integrity}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preflight-only", action="store_true", help="Validate all inputs without creating output, fitting, or scoring")
    args = parser.parse_args()
    if args.preflight_only:
        preflight = run_preflight()
        print(json.dumps({"status": "PREFLIGHT_PASS", "summary": preflight["summary"], "configuration_sha256": preflight["configuration_sha256"]}, indent=2))
        return
    result = run_experiment6()
    print(json.dumps({"metrics": result["metrics"], "family_metrics": result["family_metrics"], "decision": result["decision"]}, indent=2))


if __name__ == "__main__":
    main()

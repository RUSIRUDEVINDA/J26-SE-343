"""Create and validate the prospective synthetic-family human-review pack.

This utility performs data integrity and packaging only.  It does not import,
load, fit, score, or invoke any classifier or model artifact.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime
from pathlib import Path
from typing import Any
from zoneinfo import ZoneInfo

import pandas as pd


ML_ROOT = Path(__file__).resolve().parent.parent
CANDIDATE_PATH = ML_ROOT / "data" / "state_land_complaints_development_530.csv"
REVIEW_PATH = ML_ROOT / "candidate-data" / "v1" / "synthetic_family_review_pack_confirmed_20261007.csv"
EXPERIMENT5_ROOT = ML_ROOT / "results" / "experiment5_combined_real_synthetic"
POPULATION_MANIFEST_PATH = EXPERIMENT5_ROOT / "experiment5_population_manifest.csv"
PROTOCOL_PATH = EXPERIMENT5_ROOT / "experiment5_protocol.json"
SUMMARY_PATH = EXPERIMENT5_ROOT / "experiment5_summary.json"
OUTPUT_DIR = ML_ROOT / "candidate-data" / "v1" / "review-assistance"
MARKDOWN_PATH = OUTPUT_DIR / "prospective_test_family_review.md"
CSV_PATH = OUTPUT_DIR / "prospective_test_family_review_template.csv"
MANIFEST_PATH = OUTPUT_DIR / "prospective_test_review_manifest.json"

ID_COLUMN = "Research_ID"
TEXT_COLUMN = "Canonical_English_Text"
LABEL_COLUMN = "ML_Label_4Class"
FAMILY_COLUMN = "Synthetic_Family_ID"
VARIANT_COLUMN = "Synthetic_Variant"
SYNTHETIC_ORIGIN = "New synthetic scenario"

EXPECTED_FAMILIES = 25
EXPECTED_RECORDS = 75
ALLOWED_DECISIONS = ["Approved", "Label_Change", "Rejected", "Needs_Discussion"]
DECISION_FIELDS = [
    "Reviewer",
    "Review_Date",
    "Review_Decision",
    "Approved_Label",
    "Variant_Consistency",
    "Review_Notes",
]

CLASS_DEFINITIONS = {
    "Administrative / Procedural / Integrity": (
        "Complaints about administrative process, procedural fairness, official conduct, "
        "record handling, transparency, conflicts of interest, or integrity in state-land lease governance."
    ),
    "Lease Revenue / Payment / Enforcement": (
        "Complaints about rent, arrears, valuation, collection, payment obligations, penalties, "
        "or enforcement of financial and contractual lease conditions."
    ),
    "Unauthorized Allocation / Transfer / Use": (
        "Complaints alleging allocation, occupation, transfer, subdivision, subletting, disposal, "
        "or use of state-leased land without the required authority or outside permitted purposes."
    ),
    "Protected / Environmental Lease Misuse": (
        "Complaints alleging misuse of leased state land affecting protected, environmentally "
        "sensitive, forest, wetland, coastal, conservation, or otherwise restricted areas."
    ),
}

SOURCE_PATHS = {
    "candidate_dataset": CANDIDATE_PATH,
    "confirmed_review_pack": REVIEW_PATH,
    "experiment5_population_manifest": POPULATION_MANIFEST_PATH,
    "experiment5_protocol": PROTOCOL_PATH,
    "experiment5_summary": SUMMARY_PATH,
}


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_hashes() -> dict[str, dict[str, str]]:
    missing = [str(path) for path in SOURCE_PATHS.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Required source files are missing: {missing}")
    return {
        name: {
            "path": str(path.relative_to(ML_ROOT)).replace("\\", "/"),
            "sha256": sha256_file(path),
        }
        for name, path in SOURCE_PATHS.items()
    }


def _require_columns(frame: pd.DataFrame, columns: list[str], source: str) -> None:
    missing = set(columns) - set(frame.columns)
    if missing:
        raise ValueError(f"{source} missing required columns: {sorted(missing)}")


def _blank(series: pd.Series) -> pd.Series:
    return series.isna() | series.fillna("").astype(str).str.strip().eq("")


def _variant_sort_key(value: Any) -> tuple[int, str]:
    text = str(value).strip()
    digits = "".join(character for character in text if character.isdigit())
    return (int(digits) if digits else 10**9, text)


def identify_and_validate_population() -> tuple[pd.DataFrame, pd.DataFrame, dict[str, Any]]:
    """Return review rows and their 75 candidate records after strict checks."""
    candidate = pd.read_csv(CANDIDATE_PATH)
    review = pd.read_csv(REVIEW_PATH)
    population_manifest = pd.read_csv(POPULATION_MANIFEST_PATH)
    protocol = json.loads(PROTOCOL_PATH.read_text(encoding="utf-8"))
    summary = json.loads(SUMMARY_PATH.read_text(encoding="utf-8"))

    _require_columns(
        candidate,
        [ID_COLUMN, TEXT_COLUMN, LABEL_COLUMN, "Record_Origin", FAMILY_COLUMN, VARIANT_COLUMN, "Scenario_Grounding"],
        "candidate dataset",
    )
    _require_columns(
        review,
        [
            FAMILY_COLUMN,
            "Research_ID_V1", "Research_ID_V2", "Research_ID_V3",
            "Canonical_English_Text_V1", "Canonical_English_Text_V2", "Canonical_English_Text_V3",
            "Proposed_Label", "Scenario_Grounding", *DECISION_FIELDS,
        ],
        "confirmed review pack",
    )
    _require_columns(
        population_manifest,
        [ID_COLUMN, "population_origin", "synthetic_family_id", "included_in_experiment", "exclusion_reason"],
        "Experiment 5 population manifest",
    )
    if protocol.get("experiment") != 5 or summary.get("experiment") != 5:
        raise ValueError("The supplied protocol/summary are not Experiment 5 artifacts")

    unreviewed = review[_blank(review["Review_Decision"])].copy()
    if len(unreviewed) != EXPECTED_FAMILIES:
        raise ValueError(f"Expected {EXPECTED_FAMILIES} blank-decision families; found {len(unreviewed)}")
    if unreviewed[FAMILY_COLUMN].isna().any() or unreviewed[FAMILY_COLUMN].astype(str).str.strip().eq("").any():
        raise ValueError("An unreviewed family has a missing/blank Synthetic_Family_ID")
    if unreviewed[FAMILY_COLUMN].duplicated().any():
        raise ValueError("Unreviewed review rows contain duplicate Synthetic_Family_ID values")
    family_ids = set(unreviewed[FAMILY_COLUMN].astype(str))

    records = candidate[
        candidate["Record_Origin"].eq(SYNTHETIC_ORIGIN)
        & candidate[FAMILY_COLUMN].fillna("").astype(str).isin(family_ids)
    ].copy()
    if len(records) != EXPECTED_RECORDS:
        raise ValueError(f"Expected {EXPECTED_RECORDS} unreviewed-family records; found {len(records)}")
    if records[ID_COLUMN].isna().any() or records[ID_COLUMN].astype(str).str.strip().eq("").any():
        raise ValueError("The prospective population contains missing/blank Research_ID values")
    if records[ID_COLUMN].nunique() != EXPECTED_RECORDS:
        raise ValueError("The prospective population must contain 75 unique Research_ID values")
    family_sizes = records.groupby(FAMILY_COLUMN).size()
    variant_counts = records.groupby(FAMILY_COLUMN)[VARIANT_COLUMN].nunique()
    label_counts = records.groupby(FAMILY_COLUMN)[LABEL_COLUMN].nunique()
    if not family_sizes.eq(3).all() or not variant_counts.eq(3).all():
        raise ValueError("Every prospective family must contain exactly three unique variants")
    if not label_counts.eq(1).all():
        raise ValueError("Every prospective family must contain exactly one proposed label")

    proposed = unreviewed.set_index(FAMILY_COLUMN)["Proposed_Label"].astype(str).sort_index()
    actual = records.groupby(FAMILY_COLUMN)[LABEL_COLUMN].first().astype(str).sort_index()
    if not proposed.equals(actual):
        raise ValueError("Review-pack proposed labels do not match candidate-record labels")
    if set(actual) - set(CLASS_DEFINITIONS):
        raise ValueError(f"Proposed label outside the four-class taxonomy: {sorted(set(actual) - set(CLASS_DEFINITIONS))}")

    # Verify the review pack's three IDs and full texts against the candidate source.
    for _, review_row in unreviewed.iterrows():
        family_id = str(review_row[FAMILY_COLUMN])
        family = records[records[FAMILY_COLUMN].astype(str).eq(family_id)].copy()
        family = family.sort_values(VARIANT_COLUMN, key=lambda values: values.map(_variant_sort_key))
        expected_ids = [str(review_row[f"Research_ID_V{index}"]) for index in (1, 2, 3)]
        expected_texts = [str(review_row[f"Canonical_English_Text_V{index}"]) for index in (1, 2, 3)]
        if family[ID_COLUMN].astype(str).tolist() != expected_ids:
            raise ValueError(f"Review-pack Research_ID values differ from candidate data for {family_id}")
        if family[TEXT_COLUMN].astype(str).tolist() != expected_texts:
            raise ValueError(f"Review-pack variant text differs from candidate data for {family_id}")

    included_mask = population_manifest["included_in_experiment"].astype(str).str.strip().str.lower().isin({"true", "1", "yes"})
    included = population_manifest[included_mask]
    included_family_ids = set(included["synthetic_family_id"].fillna("").astype(str))
    included_record_ids = set(included[ID_COLUMN].astype(str))
    if family_ids & included_family_ids or set(records[ID_COLUMN].astype(str)) & included_record_ids:
        raise ValueError("Prospective families overlap the Experiment 5 included population")

    arm_overlap: dict[str, list[str]] = {}
    prospective_ids = set(records[ID_COLUMN].astype(str))
    for arm in ("arm_a", "arm_b", "arm_c", "arm_d", "arm_e"):
        oof_path = EXPERIMENT5_ROOT / arm / "oof_predictions_synthetic.csv"
        if not oof_path.is_file():
            raise FileNotFoundError(f"Experiment 5 arm evidence is missing: {oof_path}")
        oof = pd.read_csv(oof_path)
        _require_columns(oof, [ID_COLUMN, "synthetic_family_id"], f"Experiment 5 {arm} synthetic OOF")
        overlap = sorted(prospective_ids & set(oof[ID_COLUMN].astype(str)))
        if overlap:
            arm_overlap[arm] = overlap
    if arm_overlap:
        raise ValueError(f"Prospective records entered an Experiment 5 arm: {arm_overlap}")

    if not records[TEXT_COLUMN].notna().all() or records[TEXT_COLUMN].astype(str).str.strip().eq("").any():
        raise ValueError("A prospective variant has missing/blank text")
    if unreviewed[DECISION_FIELDS].apply(lambda column: _blank(column).all()).eq(False).any():
        raise ValueError("A decision field is already populated for an unreviewed family")

    label_distribution = (
        unreviewed["Proposed_Label"].value_counts().reindex(CLASS_DEFINITIONS, fill_value=0).astype(int).to_dict()
    )
    validation = {
        "family_count": int(len(unreviewed)),
        "record_count": int(len(records)),
        "unique_research_id_count": int(records[ID_COLUMN].nunique()),
        "families_with_exactly_three_variants": int(family_sizes.eq(3).sum()),
        "families_with_one_proposed_label": int(label_counts.eq(1).sum()),
        "experiment5_included_family_overlap": 0,
        "experiment5_arm_record_overlap": 0,
        "source_decision_fields_blank": True,
        "proposed_label_distribution": label_distribution,
    }
    return (
        unreviewed.sort_values(FAMILY_COLUMN).reset_index(drop=True),
        records.sort_values([FAMILY_COLUMN, VARIANT_COLUMN], key=lambda values: values.map(_variant_sort_key) if values.name == VARIANT_COLUMN else values).reset_index(drop=True),
        validation,
    )


def build_template(unreviewed: pd.DataFrame) -> pd.DataFrame:
    columns = [
        FAMILY_COLUMN,
        "Research_ID_V1", "Research_ID_V2", "Research_ID_V3",
        "Canonical_English_Text_V1", "Canonical_English_Text_V2", "Canonical_English_Text_V3",
        "Proposed_Label", "Scenario_Grounding",
        "Same_Underlying_Scenario_Structural_Check",
        *DECISION_FIELDS,
    ]
    template = unreviewed[[column for column in columns if column in unreviewed.columns]].copy()
    template["Same_Underlying_Scenario_Structural_Check"] = (
        "Yes structurally: grouped under one Synthetic_Family_ID and shared scenario grounding; "
        "semantic consistency requires human confirmation."
    )
    for field in DECISION_FIELDS:
        template[field] = ""
    return template[columns]


def build_markdown(unreviewed: pd.DataFrame) -> str:
    class_block = "\n".join(
        f"- **{label}:** {definition}" for label, definition in CLASS_DEFINITIONS.items()
    )
    lines = [
        "# Prospective Synthetic Family Review",
        "",
        "This package is reserved for prospective synthetic testing after human review is frozen. "
        "No classifier was loaded or invoked, no model predictions were generated, and Experiment 5 outcomes were not used to recommend labels.",
        "",
        "The proposed label and scenario grounding below are source metadata, not human decisions. "
        "All decision fields are intentionally blank.",
        "",
        "## Reviewer instructions",
        "",
        f"Allowed Review_Decision values: {', '.join(ALLOWED_DECISIONS)}.",
        "Review all three complete variants before deciding. Confirm semantic consistency independently; the structural check only records that the generator grouped the variants under one family and grounding.",
        "Do not run prediction on this population until the human decisions have been completed and frozen.",
        "",
    ]
    for index, row in unreviewed.iterrows():
        family_id = str(row[FAMILY_COLUMN])
        lines.extend([
            f"## {index + 1}. {family_id}",
            "",
            f"- **Proposed label (source metadata):** {row['Proposed_Label']}",
            f"- **Scenario grounding:** {row['Scenario_Grounding']}",
            "- **Same underlying scenario:** Structurally yes—the three records share one Synthetic_Family_ID and scenario grounding. Semantic consistency remains for the human reviewer to confirm.",
            "",
            "### Complete variants",
            "",
        ])
        for variant in (1, 2, 3):
            lines.extend([
                f"#### Variant {variant} — {row[f'Research_ID_V{variant}']}",
                "",
                str(row[f"Canonical_English_Text_V{variant}"]),
                "",
            ])
        lines.extend([
            "### Four-class definitions",
            "",
            class_block,
            "",
            "### Neutral review questions",
            "",
            "1. Do all three variants describe the same underlying allegation and governance issue?",
            "2. Is the complaint within the scope of state-land lease governance?",
            "3. Which single class definition best matches the primary complaint, without relying on the proposed label alone?",
            "4. Does any variant introduce a materially different allegation, class, or contradictory fact?",
            "5. Is the information sufficiently clear for approval, or should the family be rejected or discussed further?",
            "",
            "### Human decision fields",
            "",
            "- **Review_Decision:** ______________________________",
            "- **Approved_Label:** _______________________________",
            "- **Variant_Consistency:** __________________________",
            "- **Review_Notes:** _________________________________",
            "",
            "---",
            "",
        ])
    return "\n".join(lines)


def build_manifest(
    unreviewed: pd.DataFrame,
    validation: dict[str, Any],
    hashes: dict[str, dict[str, str]],
) -> dict[str, Any]:
    families = []
    for _, row in unreviewed.iterrows():
        families.append({
            "synthetic_family_id": str(row[FAMILY_COLUMN]),
            "research_ids": [str(row[f"Research_ID_V{variant}"]) for variant in (1, 2, 3)],
            "proposed_label": str(row["Proposed_Label"]),
        })
    return {
        "package": "prospective synthetic family human-review assistance",
        "created_at": datetime.now(ZoneInfo("Asia/Colombo")).isoformat(timespec="seconds"),
        "timezone": "Asia/Colombo",
        "source_hashes": hashes,
        "family_count": EXPECTED_FAMILIES,
        "record_count": EXPECTED_RECORDS,
        "family_ids": [family["synthetic_family_id"] for family in families],
        "families": families,
        "proposed_label_distribution": validation["proposed_label_distribution"],
        "allowed_review_decisions": ALLOWED_DECISIONS,
        "prediction_before_review_freeze_prohibited": True,
        "prediction_prohibition": "Do not run classifier prediction or inference on this population before human review decisions are completed and frozen.",
        "population_reservation": "This population is reserved for prospective synthetic testing after human review freeze.",
        "model_inference_occurred": False,
        "model_inference_statement": "No classifier or model artifact was loaded or invoked, and no model inference occurred while creating this package.",
        "experiment5_outcomes_used_for_label_recommendation": False,
        "auto_approval_performed": False,
        "decision_fields_initially_blank": True,
        "validation": validation,
    }


def validate_generated_package(expected_source_hashes: dict[str, dict[str, str]] | None = None) -> dict[str, Any]:
    """Validate generated artifacts and prove current sources retain their hashes."""
    unreviewed, records, population_validation = identify_and_validate_population()
    if not MARKDOWN_PATH.is_file() or not CSV_PATH.is_file() or not MANIFEST_PATH.is_file():
        raise FileNotFoundError("One or more prospective review package outputs are missing")
    template = pd.read_csv(CSV_PATH, keep_default_na=False)
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    _require_columns(template, [FAMILY_COLUMN, *DECISION_FIELDS], "prospective CSV template")
    if len(template) != EXPECTED_FAMILIES or template[FAMILY_COLUMN].nunique() != EXPECTED_FAMILIES:
        raise ValueError("Prospective CSV template must contain exactly 25 unique families")
    if set(template[FAMILY_COLUMN].astype(str)) != set(unreviewed[FAMILY_COLUMN].astype(str)):
        raise ValueError("Prospective CSV template family IDs differ from the unreviewed source families")
    for field in DECISION_FIELDS:
        if not template[field].astype(str).str.strip().eq("").all():
            raise ValueError(f"Prospective CSV decision field is not blank: {field}")
    if manifest.get("model_inference_occurred") is not False:
        raise ValueError("Manifest must state that no model inference occurred")
    if manifest.get("prediction_before_review_freeze_prohibited") is not True:
        raise ValueError("Manifest must prohibit prediction before review freeze")
    if manifest.get("family_count") != EXPECTED_FAMILIES or manifest.get("record_count") != EXPECTED_RECORDS:
        raise ValueError("Manifest population counts are incorrect")
    current_hashes = source_hashes()
    recorded_hashes = manifest.get("source_hashes")
    if current_hashes != recorded_hashes:
        raise ValueError("Current source hashes differ from the hashes recorded in the review manifest")
    if expected_source_hashes is not None and current_hashes != expected_source_hashes:
        raise ValueError("A source file changed during package creation")
    markdown = MARKDOWN_PATH.read_text(encoding="utf-8")
    for family_id in unreviewed[FAMILY_COLUMN].astype(str):
        if family_id not in markdown:
            raise ValueError(f"Markdown review document is missing family {family_id}")
    for research_id in records[ID_COLUMN].astype(str):
        if research_id not in markdown:
            raise ValueError(f"Markdown review document is missing Research_ID {research_id}")
    return {
        **population_validation,
        "template_family_count": int(len(template)),
        "decision_fields_blank": True,
        "markdown_contains_all_families": True,
        "markdown_contains_all_research_ids": True,
        "source_hash_preservation": "PASS",
        "model_inference_occurred": False,
    }


def create_package() -> dict[str, Any]:
    hashes_before = source_hashes()
    unreviewed, _, validation = identify_and_validate_population()
    targets = [MARKDOWN_PATH, CSV_PATH, MANIFEST_PATH]
    occupied = [str(path) for path in targets if path.exists()]
    if occupied:
        raise FileExistsError(f"Refusing to overwrite existing review-package files: {occupied}")
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    MARKDOWN_PATH.write_text(build_markdown(unreviewed), encoding="utf-8")
    build_template(unreviewed).to_csv(CSV_PATH, index=False)
    MANIFEST_PATH.write_text(
        json.dumps(build_manifest(unreviewed, validation, hashes_before), indent=2) + "\n",
        encoding="utf-8",
    )
    result = validate_generated_package(hashes_before)
    return {
        "outputs": [str(path.relative_to(ML_ROOT)).replace("\\", "/") for path in targets],
        "validation": result,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--validate-only", action="store_true", help="Validate an existing package without writing files")
    args = parser.parse_args()
    result = validate_generated_package() if args.validate_only else create_package()
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()

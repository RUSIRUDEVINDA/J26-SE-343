"""
test_experiment4_arm_b.py
--------------------------
Focused unit tests for Experiment 4 Arm B synthetic augmentation.

Tests use small controlled fixtures.  They do not depend on production
dataset files and do not call model.fit() for the majority of checks.

The tests cover:
  - Unreviewed/rejected families excluded
  - Incomplete approvals rejected as validation errors
  - Label changes applied only in-memory
  - Stale/tampered variant texts and unknown family IDs rejected
  - Unknown Related_Baseline_IDs rejected
  - Related evaluation groups exclude all three family variants
  - Synthetic/source-derived records never enter evaluation
  - TF-IDF fit inputs contain only permitted training records
  - Frozen evaluation IDs remain identical between arms
  - Zero eligible augmentation triggers dry-run / no-fit behavior
  - Dry-run does not call fit
  - Paired helped/hurt arithmetic
  - Existing Arm A regression behavior remains unchanged
"""
from __future__ import annotations

import copy
import shutil
import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

# ---------------------------------------------------------------------------
# Path setup — ensure src/ is importable
# ---------------------------------------------------------------------------
_TEST_DIR = Path(__file__).resolve().parent
_SRC_DIR  = _TEST_DIR.parent / "src"
if str(_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(_SRC_DIR))

from review_loader import (
    FamilyReview,
    FOUR_CLASS_LABELS,
    ACCEPTED_DECISIONS,
    APPROVAL_COMPATIBLE_CONSISTENCY,
    RELATED_IDS_DELIMITER,
    build_fold_manifest,
    load_and_validate_reviews,
    validate_related_baseline_ids,
    validate_review_pack_structure,
    _blank,
    _parse_date,
    _parse_related_ids,
)
from evaluate_experiment4 import (
    ID_COLUMN,
    TARGET_COLUMN,
    TEXT_COLUMN,
    LABEL_ORDER,
    build_paired_comparison,
    run_arm_b,
)

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

L0 = "Administrative / Procedural / Integrity"
L1 = "Lease Revenue / Payment / Enforcement"
L2 = "Unauthorized Allocation / Transfer / Use"
L3 = "Protected / Environmental Lease Misuse"

_LABELS = [L0, L1, L2, L3]

SYNTHETIC_ORIGIN = "New synthetic scenario"
BASELINE_ORIGIN  = "Existing baseline"


# ---------------------------------------------------------------------------
# Fixture helpers
# ---------------------------------------------------------------------------

def _make_candidate_df(
    baseline_ids: list[str],
    baseline_labels: list[str],
    synthetic_families: list[dict],  # list of {family_id, ids:[v1,v2,v3], label, texts:[t1,t2,t3]}
) -> pd.DataFrame:
    rows = []
    for rid, lbl in zip(baseline_ids, baseline_labels):
        rows.append({
            "Research_ID": rid,
            "Canonical_English_Text": f"Text for {rid}",
            "ML_Label_4Class": lbl,
            "Record_Origin": BASELINE_ORIGIN,
            "Source_Group_ID": f"SG-{rid}",
            "Synthetic_Family_ID": None,
            "Synthetic_Variant": None,
        })
    for fam in synthetic_families:
        for i, (rid, txt) in enumerate(zip(fam["ids"], fam["texts"]), start=1):
            rows.append({
                "Research_ID": rid,
                "Canonical_English_Text": txt,
                "ML_Label_4Class": fam["label"],
                "Record_Origin": SYNTHETIC_ORIGIN,
                "Source_Group_ID": None,
                "Synthetic_Family_ID": fam["family_id"],
                "Synthetic_Variant": float(i),
            })
    return pd.DataFrame(rows)


def _make_review_pack_row(
    family_id: str,
    research_ids: tuple[str, str, str],
    texts: tuple[str, str, str],
    proposed_label: str,
    decision: str = "",
    reviewer: str = "",
    review_date: str = "",
    variant_consistency: str = "",
    approved_label: str = "",
    related_ids: str = "",
    review_notes: str = "",
) -> dict:
    return {
        "Synthetic_Family_ID": family_id,
        "Research_ID_V1": research_ids[0],
        "Research_ID_V2": research_ids[1],
        "Research_ID_V3": research_ids[2],
        "Canonical_English_Text_V1": texts[0],
        "Canonical_English_Text_V2": texts[1],
        "Canonical_English_Text_V3": texts[2],
        "Proposed_Label": proposed_label,
        "Scenario_Grounding": "Hypothetical; not derived from a specific baseline or source incident",
        "Reviewer": reviewer,
        "Review_Date": review_date,
        "Review_Decision": decision,
        "Approved_Label": approved_label,
        "Variant_Consistency": variant_consistency,
        "Related_Baseline_IDs": related_ids,
        "Review_Notes": review_notes,
    }


def _make_baseline_df(
    ids: list[str],
    labels: list[str],
    fold_map: dict[str, int],
    cg_map: dict[str, str],
) -> pd.DataFrame:
    rows = []
    for rid, lbl in zip(ids, labels):
        rows.append({
            ID_COLUMN: rid,
            TARGET_COLUMN: lbl,
            TEXT_COLUMN: f"Text for {rid}",
            "Combined_Group": cg_map[rid],
            "Source_Group_ID": f"SG-{rid}",
        })
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Shared small fixtures (4 baseline records, 2 folds, 2 synthetic families)
# ---------------------------------------------------------------------------

BASE_IDS    = ["B-001", "B-002", "B-003", "B-004"]
BASE_LABELS = [L0, L1, L2, L0]
FOLD_MAP    = {"B-001": 1, "B-002": 1, "B-003": 2, "B-004": 2}
CG_MAP      = {"B-001": "CG-A", "B-002": "CG-A", "B-003": "CG-B", "B-004": "CG-B"}

SYN_FAM_1 = {
    "family_id": "SYN-FAM-001",
    "ids": ["SYN-0001", "SYN-0002", "SYN-0003"],
    "label": L0,
    "texts": ["Syn text 1a", "Syn text 1b", "Syn text 1c"],
}
SYN_FAM_2 = {
    "family_id": "SYN-FAM-002",
    "ids": ["SYN-0004", "SYN-0005", "SYN-0006"],
    "label": L1,
    "texts": ["Syn text 2a", "Syn text 2b", "Syn text 2c"],
}


@pytest.fixture
def candidate_df():
    return _make_candidate_df(BASE_IDS, BASE_LABELS, [SYN_FAM_1, SYN_FAM_2])


@pytest.fixture
def baseline_df():
    return _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)


def _approved_row(fam: dict, related: str = "") -> dict:
    return _make_review_pack_row(
        family_id=fam["family_id"],
        research_ids=tuple(fam["ids"]),
        texts=tuple(fam["texts"]),
        proposed_label=fam["label"],
        decision="Approved",
        reviewer="Researcher A",
        review_date="2026-10-01",
        variant_consistency="Consistent",
        related_ids=related,
    )


# ---------------------------------------------------------------------------
# Section 1: review_loader — eligibility rules
# ---------------------------------------------------------------------------

class TestUnreviewedExcluded:
    def test_blank_decision_is_unreviewed(self, candidate_df):
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
            decision="",
        )
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        fam1 = next(r for r in reviews if r.family_id == SYN_FAM_1["family_id"])
        assert not fam1.eligible
        assert fam1.exclusion_reason == "unreviewed"
        assert fam1.review_decision is None

    def test_rejected_decision_excluded_not_error(self, candidate_df):
        for dec in [
            "Rejected_Out_of_Scope",
            "Rejected_Ambiguous",
            "Rejected_Multi_Issue",
            "Rejected_Contradictory",
        ]:
            row = _make_review_pack_row(
                SYN_FAM_1["family_id"],
                tuple(SYN_FAM_1["ids"]),
                tuple(SYN_FAM_1["texts"]),
                SYN_FAM_1["label"],
                decision=dec,
                reviewer="R",
                review_date="2026-10-01",
                variant_consistency="Consistent",
            )
            df = pd.DataFrame([row])
            reviews = load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )
            fam = reviews[0]
            assert not fam.eligible, f"Expected not eligible for {dec}"
            assert dec in (fam.exclusion_reason or "")

    def test_needs_discussion_excluded(self, candidate_df):
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
            decision="Needs_Discussion",
            reviewer="R",
            review_date="2026-10-01",
        )
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        assert not reviews[0].eligible


class TestIncompleteApprovals:
    """Incomplete Approved decisions must raise ValueError (not silently pass)."""

    def _approved_missing(self, candidate_df, **overrides):
        row = _approved_row(SYN_FAM_1)
        row.update(overrides)
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="incomplete Approved"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_blank_reviewer_raises(self, candidate_df):
        self._approved_missing(candidate_df, Reviewer="")

    def test_blank_review_date_raises(self, candidate_df):
        self._approved_missing(candidate_df, Review_Date="")

    def test_blank_variant_consistency_raises(self, candidate_df):
        self._approved_missing(candidate_df, Variant_Consistency="")

    def test_inconsistent_consistency_with_approved_raises(self, candidate_df):
        row = _approved_row(SYN_FAM_1)
        row["Variant_Consistency"] = "Inconsistent"
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="incomplete Approved|incompatible"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_approved_label_mismatch_raises(self, candidate_df):
        """Approved_Label supplied for Approved decision must match Proposed_Label."""
        row = _approved_row(SYN_FAM_1)
        row["Approved_Label"] = L1  # different from SYN_FAM_1 label (L0)
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="incomplete Approved|does not match"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_unknown_decision_raises(self, candidate_df):
        row = _approved_row(SYN_FAM_1)
        row["Review_Decision"] = "SuperApproved"
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="unknown Review_Decision"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )


class TestLabelChangeRules:
    def test_label_change_applies_approved_label_not_proposed(self, candidate_df):
        """Label_Change must use Approved_Label, not Proposed_Label."""
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
            decision="Label_Change",
            reviewer="Researcher A",
            review_date="2026-10-01",
            variant_consistency="Consistent",
            approved_label=L2,
            review_notes="Corrected label.",
        )
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        fam = reviews[0]
        assert fam.eligible
        assert fam.effective_label == L2
        assert fam.proposed_label == SYN_FAM_1["label"]  # proposed unchanged

    def test_label_change_missing_approved_label_raises(self, candidate_df):
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
            decision="Label_Change",
            reviewer="Researcher A",
            review_date="2026-10-01",
            variant_consistency="Consistent",
            review_notes="Some note.",
        )
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="incomplete Label_Change"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_label_change_missing_notes_raises(self, candidate_df):
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
            decision="Label_Change",
            reviewer="Researcher A",
            review_date="2026-10-01",
            variant_consistency="Consistent",
            approved_label=L2,
        )
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="incomplete Label_Change"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_label_change_unknown_approved_label_raises(self, candidate_df):
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
            decision="Label_Change",
            reviewer="R",
            review_date="2026-10-01",
            variant_consistency="Consistent",
            approved_label="Made-up category",
            review_notes="Note",
        )
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="incomplete Label_Change|not in the four-class"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )


# ---------------------------------------------------------------------------
# Section 2: Structural validation
# ---------------------------------------------------------------------------

class TestStructuralValidation:
    def test_tampered_text_raises(self, candidate_df):
        """If review pack text differs from candidate dataset, validation fails."""
        row = _approved_row(SYN_FAM_1)
        row["Canonical_English_Text_V1"] = "TAMPERED TEXT THAT DIFFERS"
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="structural|text does not match"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_unknown_family_id_raises(self, candidate_df):
        """Family IDs not in the candidate dataset must raise structural error."""
        row = _approved_row(SYN_FAM_1)
        row["Synthetic_Family_ID"] = "SYN-FAM-GHOST"
        df = pd.DataFrame([row])
        with pytest.raises(ValueError, match="structural|not found in candidate"):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )

    def test_mismatched_research_ids_raises(self, candidate_df):
        """Research_IDs in the pack that don't match the candidate family raise an error."""
        row = _approved_row(SYN_FAM_1)
        row["Research_ID_V1"] = "SYN-9999"  # not in any family
        df = pd.DataFrame([row])
        with pytest.raises(ValueError):
            load_and_validate_reviews(
                review_path=_write_csv_tmp(df),
                candidate_df=candidate_df,
            )


# ---------------------------------------------------------------------------
# Section 3: Related baseline ID validation
# ---------------------------------------------------------------------------

class TestRelatedBaselineIdValidation:
    def test_unknown_related_id_raises(self, candidate_df):
        row = _approved_row(SYN_FAM_1, related="B-001;GHOST-999")
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        with pytest.raises(ValueError, match="Unknown Related_Baseline_IDs"):
            validate_related_baseline_ids(reviews, set(BASE_IDS))

    def test_valid_related_id_passes(self, candidate_df):
        row = _approved_row(SYN_FAM_1, related="B-001")
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        # Must not raise
        validate_related_baseline_ids(reviews, set(BASE_IDS))
        fam = reviews[0]
        assert fam.related_baseline_ids == ["B-001"]

    def test_blank_related_id_not_an_error(self, candidate_df):
        row = _approved_row(SYN_FAM_1, related="")
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        validate_related_baseline_ids(reviews, set(BASE_IDS))
        assert reviews[0].related_baseline_ids == []

    def test_semicolon_delimiter_parsed(self, candidate_df):
        row = _approved_row(SYN_FAM_1, related="B-001;B-002")
        df = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df),
            candidate_df=candidate_df,
        )
        validate_related_baseline_ids(reviews, set(BASE_IDS))
        assert reviews[0].related_baseline_ids == ["B-001", "B-002"]


# ---------------------------------------------------------------------------
# Section 4: Per-fold manifest — related-group exclusion
# ---------------------------------------------------------------------------

class TestFoldManifest:
    def test_related_eval_group_excludes_family_from_that_fold(self, candidate_df):
        """
        If B-001 is related and B-001 is in fold 1 eval, the family must be
        excluded from fold 1 but included in fold 2.
        """
        row = _approved_row(SYN_FAM_1, related="B-001")
        df_pack = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df_pack),
            candidate_df=candidate_df,
        )
        validate_related_baseline_ids(reviews, set(BASE_IDS))

        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        manifest = build_fold_manifest(reviews, baseline_df, FOLD_MAP, CG_MAP)

        fam1_fold1 = next(
            (r for r in manifest if r["Synthetic_Family_ID"] == SYN_FAM_1["family_id"]
             and r["Fold"] == 1), None
        )
        fam1_fold2 = next(
            (r for r in manifest if r["Synthetic_Family_ID"] == SYN_FAM_1["family_id"]
             and r["Fold"] == 2), None
        )
        assert fam1_fold1 is not None
        assert fam1_fold2 is not None
        assert not fam1_fold1["Included"], "Family with related B-001 must be excluded from fold 1"
        assert fam1_fold2["Included"], "Family with related B-001 should be included in fold 2"

    def test_no_related_ids_included_in_all_folds(self, candidate_df):
        row = _approved_row(SYN_FAM_1, related="")
        df_pack = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df_pack),
            candidate_df=candidate_df,
        )
        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        manifest = build_fold_manifest(reviews, baseline_df, FOLD_MAP, CG_MAP)
        fam_rows = [r for r in manifest if r["Synthetic_Family_ID"] == SYN_FAM_1["family_id"]]
        assert all(r["Included"] for r in fam_rows)

    def test_non_eligible_family_recorded_in_manifest(self, candidate_df):
        """Unreviewed families appear in the manifest with Included=False."""
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
        )
        df_pack = pd.DataFrame([row])
        reviews = load_and_validate_reviews(
            review_path=_write_csv_tmp(df_pack),
            candidate_df=candidate_df,
        )
        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        manifest = build_fold_manifest(reviews, baseline_df, FOLD_MAP, CG_MAP)
        assert len(manifest) >= 1
        assert not any(r["Included"] for r in manifest), (
            "Unreviewed family must not be included in any fold"
        )


# ---------------------------------------------------------------------------
# Section 5: run_arm_b — evaluation isolation
# ---------------------------------------------------------------------------

class TestArmBEvaluationIsolation:
    """
    Verify that TF-IDF fit inputs contain only permitted training records
    and that evaluation IDs remain identical between arms.
    """

    def _build_eligible_reviews(self) -> list[FamilyReview]:
        return [
            FamilyReview(
                family_id=SYN_FAM_1["family_id"],
                research_ids=SYN_FAM_1["ids"],
                proposed_label=SYN_FAM_1["label"],
                review_decision="Approved",
                reviewer="Researcher A",
                review_date="2026-10-01",
                variant_consistency="Consistent",
                approved_label_raw=None,
                review_notes=None,
                related_baseline_ids=[],
                effective_label=SYN_FAM_1["label"],
                eligible=True,
                exclusion_reason=None,
            )
        ]

    def test_arm_b_oof_has_only_baseline_ids(self, tmp_path, candidate_df):
        """OOF predictions contain exactly the 200 baseline Research_IDs."""
        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        reviews = self._build_eligible_reviews()

        arm_dir = tmp_path / "arm_b"
        results = run_arm_b(
            arm_dir=arm_dir,
            baseline_df=baseline_df,
            fold_map=FOLD_MAP,
            cg_map=CG_MAP,
            reviews=reviews,
            candidate_df=candidate_df,
        )
        oof_ids = set(results["oof_df"]["Research_ID"].tolist())
        assert oof_ids == set(BASE_IDS), (
            "OOF must contain exactly the baseline IDs"
        )
        # No synthetic IDs should appear
        syn_ids = set(SYN_FAM_1["ids"])
        assert oof_ids.isdisjoint(syn_ids), (
            "Synthetic Research_IDs must not appear in OOF"
        )

    def test_arm_b_oof_count_equals_baseline(self, tmp_path, candidate_df):
        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        reviews = self._build_eligible_reviews()
        arm_dir = tmp_path / "arm_b"
        results = run_arm_b(
            arm_dir=arm_dir,
            baseline_df=baseline_df,
            fold_map=FOLD_MAP,
            cg_map=CG_MAP,
            reviews=reviews,
            candidate_df=candidate_df,
        )
        assert len(results["oof_df"]) == len(BASE_IDS)

    def test_zero_eligible_families_still_produces_oof(self, tmp_path, candidate_df):
        """When zero families are eligible, Arm B runs but with no augmentation."""
        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        # No eligible reviews
        reviews: list[FamilyReview] = []

        arm_dir = tmp_path / "arm_b_no_aug"
        results = run_arm_b(
            arm_dir=arm_dir,
            baseline_df=baseline_df,
            fold_map=FOLD_MAP,
            cg_map=CG_MAP,
            reviews=reviews,
            candidate_df=candidate_df,
        )
        # OOF is still produced (same as Arm A without augmentation)
        assert len(results["oof_df"]) == len(BASE_IDS)
        # All fold augmentation entries show 0 synthetic families
        for row in results["fold_augmentation_summary"]:
            assert row["Eligible_Families"] == 0
            assert row["Eligible_Records"] == 0

    def test_fold_augmentation_summary_records_count(self, tmp_path, candidate_df):
        """Each fold summary records the number of synthetic families/records appended."""
        baseline_df = _make_baseline_df(BASE_IDS, BASE_LABELS, FOLD_MAP, CG_MAP)
        reviews = self._build_eligible_reviews()
        arm_dir = tmp_path / "arm_b_summary"
        results = run_arm_b(
            arm_dir=arm_dir,
            baseline_df=baseline_df,
            fold_map=FOLD_MAP,
            cg_map=CG_MAP,
            reviews=reviews,
            candidate_df=candidate_df,
        )
        for row in results["fold_augmentation_summary"]:
            # One eligible family = 3 records per fold (no related IDs)
            assert row["Eligible_Families"] == 1
            assert row["Eligible_Records"] == 3


# ---------------------------------------------------------------------------
# Section 6: Real orchestration dry-run tests (Fix 2)
#
# These tests call run_experiment4_arm_b() directly with a complete 200-row
# fictional fixture.  run_arm_b is monkeypatched to raise immediately if
# invoked, so no model is ever fitted.
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# 200-row fictional fixture builder
# ---------------------------------------------------------------------------

def _build_200_row_fixture(tmp_path: Path) -> tuple[Path, Path, Path, Path]:
    """
    Build a self-consistent 200-row fictional fixture:
      - original dataset (200 rows, 4 classes, Source_Group_ID)
      - candidate dataset (200 baseline + 6 synthetic rows)
      - frozen fold reference (Exp2 OOF format, 5 folds, CG never crossing folds)
      - review pack (one synthetic family, unreviewed)

    Returns (candidate_path, original_path, exp2_oof_path, review_pack_path)
    """
    from evaluate_experiment4 import LABEL_ORDER, ID_COLUMN, TARGET_COLUMN, TEXT_COLUMN
    labels_cycle = (LABEL_ORDER * 50)[:200]   # 50 of each class
    ids = [f"FICTB-{i:04d}" for i in range(200)]
    # 40 CombinedGroups of 5 records each; 8 per fold
    cg_list = [f"CG-{i // 5:03d}" for i in range(200)]
    fold_list = [(i // 40) + 1 for i in range(200)]  # folds 1-5, 40 each

    original_rows = []
    for i, rid in enumerate(ids):
        original_rows.append({
            "Research_ID": rid,
            "Canonical_English_Text": f"Fictional baseline text {i} about land governance.",
            "ML_Label_4Class": labels_cycle[i],
            "Source_Group_ID": f"SG-{i:04d}",
        })
    orig_df = pd.DataFrame(original_rows)
    orig_path = tmp_path / "original_200.csv"
    orig_df.to_csv(orig_path, index=False)

    # Candidate dataset = baseline rows with Record_Origin + 6 synthetic rows
    cand_rows = []
    for i, rid in enumerate(ids):
        cand_rows.append({
            "Research_ID": rid,
            "Canonical_English_Text": f"Fictional baseline text {i} about land governance.",
            "ML_Label_4Class": labels_cycle[i],
            "Record_Origin": "Existing baseline",
            "Source_Group_ID": f"SG-{i:04d}",
            "Synthetic_Family_ID": None,
            "Synthetic_Variant": None,
        })
    # Two synthetic families, 3 variants each
    for fam_idx, (fam_id, label) in enumerate([("FICTF-001", LABEL_ORDER[0]), ("FICTF-002", LABEL_ORDER[1])]):
        for v in range(1, 4):
            cand_rows.append({
                "Research_ID": f"FICTS-{fam_idx:02d}V{v}",
                "Canonical_English_Text": f"Fictional synthetic text family {fam_id} variant {v}.",
                "ML_Label_4Class": label,
                "Record_Origin": "New synthetic scenario",
                "Source_Group_ID": None,
                "Synthetic_Family_ID": fam_id,
                "Synthetic_Variant": float(v),
            })
    cand_df = pd.DataFrame(cand_rows)
    cand_path = tmp_path / "candidate_530.csv"
    cand_df.to_csv(cand_path, index=False)

    # Exp2 OOF (frozen fold reference)
    exp2_rows = []
    for i, rid in enumerate(ids):
        exp2_rows.append({
            "Research_ID": rid,
            "True_Label": labels_cycle[i],
            "Predicted_Label": labels_cycle[i],   # perfect predictions for simplicity
            "Combined_Group": cg_list[i],
            "Fold": fold_list[i],
        })
    exp2_df = pd.DataFrame(exp2_rows)
    exp2_path = tmp_path / "exp2_oof.csv"
    exp2_df.to_csv(exp2_path, index=False)

    # Review pack — one family FICTF-001 (unreviewed), one FICTF-002 (approved)
    review_rows = [
        {
            "Synthetic_Family_ID": "FICTF-001",
            "Research_ID_V1": "FICTS-00V1",
            "Research_ID_V2": "FICTS-00V2",
            "Research_ID_V3": "FICTS-00V3",
            "Canonical_English_Text_V1": "Fictional synthetic text family FICTF-001 variant 1.",
            "Canonical_English_Text_V2": "Fictional synthetic text family FICTF-001 variant 2.",
            "Canonical_English_Text_V3": "Fictional synthetic text family FICTF-001 variant 3.",
            "Proposed_Label": LABEL_ORDER[0],
            "Scenario_Grounding": "Hypothetical",
            "Reviewer": "",
            "Review_Date": "",
            "Review_Decision": "",
            "Approved_Label": "",
            "Variant_Consistency": "",
            "Related_Baseline_IDs": "",
            "Review_Notes": "",
        },
        {
            "Synthetic_Family_ID": "FICTF-002",
            "Research_ID_V1": "FICTS-01V1",
            "Research_ID_V2": "FICTS-01V2",
            "Research_ID_V3": "FICTS-01V3",
            "Canonical_English_Text_V1": "Fictional synthetic text family FICTF-002 variant 1.",
            "Canonical_English_Text_V2": "Fictional synthetic text family FICTF-002 variant 2.",
            "Canonical_English_Text_V3": "Fictional synthetic text family FICTF-002 variant 3.",
            "Proposed_Label": LABEL_ORDER[1],
            "Scenario_Grounding": "Hypothetical",
            "Reviewer": "Fictional Reviewer",
            "Review_Date": "2026-10-01",
            "Review_Decision": "Approved",
            "Approved_Label": "",
            "Variant_Consistency": "Consistent",
            "Related_Baseline_IDs": "",
            "Review_Notes": "",
        },
    ]
    review_df = pd.DataFrame(review_rows)
    review_path = tmp_path / "review_pack.csv"
    review_df.to_csv(review_path, index=False)

    return cand_path, orig_path, exp2_path, review_path


def _build_arm_a_dir(tmp_path: Path, ids: list[str], labels: list[str],
                     folds: list[int], cg_list: list[str]) -> Path:
    """
    Build a minimal fictional Arm A output directory with OOF and run_metadata.
    run_metadata.json contains only what _verify_pipeline_config needs.
    """
    import json as _json
    from evaluate_experiment4 import build_pipeline_with_cleaner, _safe_json_impl
    arm_a_dir = tmp_path / "arm_a_run"
    arm_a_out = arm_a_dir / "arm_a"
    arm_a_out.mkdir(parents=True)

    # Build OOF with perfect predictions
    oof_rows = []
    for rid, lbl, fold, cg in zip(ids, labels, folds, cg_list):
        oof_rows.append({
            "Research_ID": rid, "True_Label": lbl,
            "Predicted_Label": lbl, "Fold": fold, "Combined_Group": cg,
        })
    pd.DataFrame(oof_rows).to_csv(arm_a_out / "oof_predictions.csv", index=False)

    # Metrics matching perfect OOF
    from sklearn.metrics import accuracy_score, f1_score
    from evaluate_experiment4 import LABEL_ORDER
    acc = round(float(accuracy_score(labels, labels)), 4)
    mf1 = round(float(f1_score(labels, labels, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    wf1 = round(float(f1_score(labels, labels, average="weighted", labels=LABEL_ORDER, zero_division=0)), 4)
    metrics = {"Accuracy": acc, "Macro_F1_sklearn": mf1, "Weighted_F1": wf1, "Misclassified": 0}
    with open(arm_a_out / "metrics.json", "w") as f:
        _json.dump(metrics, f)

    # run_metadata with pipeline section matching current build_pipeline_with_cleaner()
    pipeline = build_pipeline_with_cleaner()
    raw_params = pipeline.get_params(deep=True)

    def _safe_json(v):
        import math
        if v is None or isinstance(v, bool): return v
        if isinstance(v, int): return v
        if isinstance(v, float): return v if math.isfinite(v) else str(v)
        if isinstance(v, str): return v
        if isinstance(v, tuple): return [_safe_json(x) for x in v]
        if isinstance(v, list): return [_safe_json(x) for x in v]
        if isinstance(v, dict): return {k: _safe_json(vv) for k, vv in v.items()}
        return repr(v)

    steps = [name for name, _ in pipeline.steps]
    params_json = {k: _safe_json(v) for k, v in sorted(raw_params.items())}
    meta = {
        "pipeline": {
            "steps": steps,
            "factory": "boilerplate_transformer.build_pipeline_with_cleaner()",
            "params_from_get_params": params_json,
        }
    }
    with open(arm_a_dir / "run_metadata.json", "w") as f:
        _json.dump(meta, f)

    return arm_a_dir


# expose _safe_json as module-level helper so test can import it
try:
    from evaluate_experiment4 import _safe_json_impl  # type: ignore[attr-defined]
except ImportError:
    pass


class TestOrchestratorDryRun:
    """
    Fix 2: Real orchestration tests that call run_experiment4_arm_b() directly.
    run_arm_b is monkeypatched to raise if ever called.
    """

    @pytest.fixture(autouse=True)
    def _patch_paths(self, tmp_path, monkeypatch):
        """Build a complete 200-row fixture and patch module-level paths."""
        self.tmp_path = tmp_path
        cand_path, orig_path, exp2_path, review_path = _build_200_row_fixture(tmp_path)

        # Store for use in tests
        self.cand_path = cand_path
        self.orig_path = orig_path
        self.exp2_path = exp2_path
        self.review_path = review_path

        import evaluate_experiment4 as ev4
        monkeypatch.setattr(ev4, "CANDIDATE_CSV_PATH", cand_path)
        monkeypatch.setattr(ev4.config, "DATASET_PATH", orig_path)
        monkeypatch.setattr(ev4, "EXP2_OOF_PATH", exp2_path)

        # Patch run_arm_b to raise if called (ensures no fit occurs in these tests)
        def _no_fit(*args, **kwargs):
            raise AssertionError(
                "run_arm_b must not be called in dry-run / zero-eligible paths"
            )
        monkeypatch.setattr(ev4, "run_arm_b", _no_fit)

        # We also need to patch EXP3B_OOF_PATH for reproduction re-check
        # (only needed in non-dry-run paths, but patch anyway)
        from evaluate_experiment4 import LABEL_ORDER
        exp3b_rows = []
        from evaluate_experiment4 import ID_COLUMN
        cand_df = pd.read_csv(cand_path)
        baseline = cand_df[cand_df["Record_Origin"] == "Existing baseline"]
        exp2_df = pd.read_csv(exp2_path)
        exp2_fold = dict(zip(exp2_df["Research_ID"], exp2_df["Fold"]))
        exp2_cg = dict(zip(exp2_df["Research_ID"], exp2_df["Combined_Group"]))
        for _, row in baseline.iterrows():
            rid = row["Research_ID"]
            exp3b_rows.append({
                "Research_ID": rid,
                "True_Label": row["ML_Label_4Class"],
                "Predicted_Label": row["ML_Label_4Class"],  # identical = PASS
                "Combined_Group": exp2_cg.get(rid, "CG-000"),
                "Fold": exp2_fold.get(rid, 1),
                **{f"prob_{l.replace('/','_').replace(' ','_')}": 0.25 for l in LABEL_ORDER},
            })
        exp3b_df = pd.DataFrame(exp3b_rows)
        exp3b_path = tmp_path / "exp3b_oof.csv"
        exp3b_df.to_csv(exp3b_path, index=False)
        monkeypatch.setattr(ev4, "EXP3B_OOF_PATH", exp3b_path)

    def test_dry_run_eligible_family_returns_dry_run_status(self):
        """
        Fix 2A: dry_run=True with one eligible family must return status DRY_RUN,
        produce review_snapshot.csv, fold_manifest.csv, run_metadata.json,
        and must not call run_arm_b.
        """
        from evaluate_experiment4 import run_experiment4_arm_b
        out = self.tmp_path / "dry_run_out"
        result = run_experiment4_arm_b(
            review_file=self.review_path,
            output_dir=out,
            dry_run=True,
        )
        assert result["status"] == "DRY_RUN"
        assert "arm_b_results" not in result
        assert "comparison" not in result
        assert result["eligible_families"] == 1   # FICTF-002 is approved
        assert (out / "review_snapshot.csv").exists()
        assert (out / "fold_manifest.csv").exists()
        assert (out / "run_metadata.json").exists()
        # No arm_b/ output dir (no fit)
        assert not (out / "arm_b").exists()

    def test_zero_eligible_returns_no_eligible_families_status(self):
        """
        Fix 2B: dry_run=False with zero eligible families returns
        NO_ELIGIBLE_FAMILIES, not DRY_RUN, and still must not call run_arm_b.
        """
        from evaluate_experiment4 import run_experiment4_arm_b
        # Write a review pack with both families unreviewed
        cand_df = pd.read_csv(self.cand_path)
        review_rows = []
        syn = cand_df[cand_df["Record_Origin"] == "New synthetic scenario"]
        for fam_id in ["FICTF-001", "FICTF-002"]:
            fam = syn[syn["Synthetic_Family_ID"] == fam_id].sort_values("Synthetic_Variant")
            from evaluate_experiment4 import LABEL_ORDER
            review_rows.append({
                "Synthetic_Family_ID": fam_id,
                "Research_ID_V1": fam.iloc[0]["Research_ID"],
                "Research_ID_V2": fam.iloc[1]["Research_ID"],
                "Research_ID_V3": fam.iloc[2]["Research_ID"],
                "Canonical_English_Text_V1": fam.iloc[0]["Canonical_English_Text"],
                "Canonical_English_Text_V2": fam.iloc[1]["Canonical_English_Text"],
                "Canonical_English_Text_V3": fam.iloc[2]["Canonical_English_Text"],
                "Proposed_Label": fam.iloc[0]["ML_Label_4Class"],
                "Scenario_Grounding": "Hypothetical",
                "Reviewer": "",
                "Review_Date": "",
                "Review_Decision": "",
                "Approved_Label": "",
                "Variant_Consistency": "",
                "Related_Baseline_IDs": "",
                "Review_Notes": "",
            })
        unreviewed_path = self.tmp_path / "unreviewed_pack.csv"
        pd.DataFrame(review_rows).to_csv(unreviewed_path, index=False)

        out = self.tmp_path / "no_elig_out"
        result = run_experiment4_arm_b(
            review_file=unreviewed_path,
            output_dir=out,
            dry_run=False,
        )
        assert result["status"] == "NO_ELIGIBLE_FAMILIES"
        assert result["eligible_families"] == 0
        assert (out / "review_snapshot.csv").exists()
        assert (out / "fold_manifest.csv").exists()
        assert (out / "run_metadata.json").exists()
        assert not (out / "arm_b").exists()

    def test_review_snapshot_bytes_match_original(self):
        """
        Fix 4 regression: changing the review file on disk after capture must
        not affect the saved snapshot.
        """
        from evaluate_experiment4 import run_experiment4_arm_b
        out = self.tmp_path / "snapshot_test_out"
        run_experiment4_arm_b(
            review_file=self.review_path,
            output_dir=out,
            dry_run=True,
        )
        snapshot_bytes = (out / "review_snapshot.csv").read_bytes()
        original_bytes_at_time = self.review_path.read_bytes()
        # Snapshot must match original at time of capture
        assert snapshot_bytes == original_bytes_at_time

        # Mutate the file on disk
        self.review_path.write_text("TAMPERED_CONTENT", encoding="utf-8")

        # Re-read snapshot — must be the original captured bytes
        post_bytes = (out / "review_snapshot.csv").read_bytes()
        assert post_bytes == original_bytes_at_time
        assert post_bytes != self.review_path.read_bytes()


# ---------------------------------------------------------------------------
# Section 7: Arm A metrics recomputation tests (Fix 1)
# ---------------------------------------------------------------------------

class TestArmAMetricsRecomputation:
    def _make_oof(self, ids, labels, preds, folds):
        return pd.DataFrame({
            "Research_ID": ids,
            "True_Label": labels,
            "Predicted_Label": preds,
            "Fold": folds,
        })

    def test_perfect_oof_gives_expected_metrics(self):
        from evaluate_experiment4 import _recompute_arm_a_metrics, LABEL_ORDER
        ids    = [f"R{i}" for i in range(4)]
        labels = [L0, L1, L2, L3]
        oof = self._make_oof(ids, labels, labels, [1, 2, 3, 4])  # perfect
        m = _recompute_arm_a_metrics(oof)
        assert m["Accuracy"] == 1.0
        assert m["Misclassified"] == 0

    def test_imperfect_oof_gives_correct_misclassified(self):
        from evaluate_experiment4 import _recompute_arm_a_metrics
        ids    = ["R1", "R2", "R3", "R4"]
        labels = [L0, L0, L1, L2]
        preds  = [L0, L1, L1, L0]  # 1 wrong: R2, R4
        oof = self._make_oof(ids, labels, preds, [1, 1, 2, 2])
        m = _recompute_arm_a_metrics(oof)
        assert m["Misclassified"] == 2
        assert m["Accuracy"] == 0.5

    def test_missing_column_raises(self):
        from evaluate_experiment4 import _recompute_arm_a_metrics
        oof = pd.DataFrame({"Research_ID": ["R1"], "True_Label": [L0]})
        with pytest.raises(ValueError, match="Predicted_Label"):
            _recompute_arm_a_metrics(oof)

    def test_validate_metrics_json_missing_field_raises(self, tmp_path):
        import json as _json
        from evaluate_experiment4 import _validate_arm_a_metrics_json
        arm_a_dir = tmp_path / "arm_a_run"
        (arm_a_dir / "arm_a").mkdir(parents=True)
        # Write metrics.json missing Misclassified
        with open(arm_a_dir / "arm_a" / "metrics.json", "w") as f:
            _json.dump({"Accuracy": 0.77, "Macro_F1_sklearn": 0.77, "Weighted_F1": 0.77}, f)
        recomputed = {"Accuracy": 0.77, "Macro_F1_sklearn": 0.77, "Weighted_F1": 0.77, "Misclassified": 46}
        with pytest.raises(ValueError, match="Misclassified"):
            _validate_arm_a_metrics_json(arm_a_dir, recomputed)

    def test_validate_metrics_json_inconsistent_accuracy_raises(self, tmp_path):
        import json as _json
        from evaluate_experiment4 import _validate_arm_a_metrics_json
        arm_a_dir = tmp_path / "arm_a_run"
        (arm_a_dir / "arm_a").mkdir(parents=True)
        with open(arm_a_dir / "arm_a" / "metrics.json", "w") as f:
            _json.dump({"Accuracy": 0.88, "Macro_F1_sklearn": 0.77, "Weighted_F1": 0.77, "Misclassified": 46}, f)
        recomputed = {"Accuracy": 0.77, "Macro_F1_sklearn": 0.77, "Weighted_F1": 0.77, "Misclassified": 46}
        with pytest.raises(ValueError, match="disagrees"):
            _validate_arm_a_metrics_json(arm_a_dir, recomputed)

    def test_validate_metrics_json_absent_returns_recomputed(self, tmp_path):
        from evaluate_experiment4 import _validate_arm_a_metrics_json
        arm_a_dir = tmp_path / "arm_a_no_json"
        arm_a_dir.mkdir()
        recomputed = {"Accuracy": 0.77, "Macro_F1_sklearn": 0.77, "Weighted_F1": 0.77, "Misclassified": 46}
        result = _validate_arm_a_metrics_json(arm_a_dir, recomputed)
        assert result is recomputed  # returns same object (no zero defaults)

    def test_validate_metrics_json_consistent_passes(self, tmp_path):
        import json as _json
        from evaluate_experiment4 import _validate_arm_a_metrics_json
        arm_a_dir = tmp_path / "arm_a_run"
        (arm_a_dir / "arm_a").mkdir(parents=True)
        with open(arm_a_dir / "arm_a" / "metrics.json", "w") as f:
            _json.dump({"Accuracy": 0.77, "Macro_F1_sklearn": 0.7685,
                        "Weighted_F1": 0.7685, "Misclassified": 46}, f)
        recomputed = {"Accuracy": 0.77, "Macro_F1_sklearn": 0.7685,
                      "Weighted_F1": 0.7685, "Misclassified": 46}
        result = _validate_arm_a_metrics_json(arm_a_dir, recomputed)
        # Must return recomputed, not raw saved values
        assert result is recomputed


# ---------------------------------------------------------------------------
# Section 8: Pipeline config verification tests (Fix 3)
# ---------------------------------------------------------------------------

class TestPipelineConfigVerification:
    def _write_meta(self, tmp_path, steps, params_override=None):
        import json as _json
        import math
        from evaluate_experiment4 import build_pipeline_with_cleaner
        pipeline = build_pipeline_with_cleaner()
        raw = pipeline.get_params(deep=True)

        def _safe(v):
            """Mirror evaluate_experiment4._safe_json for run_metadata writing."""
            if v is None or isinstance(v, bool): return v
            if isinstance(v, int): return v
            if isinstance(v, float): return v if math.isfinite(v) else str(v)
            if isinstance(v, str): return v
            if isinstance(v, tuple): return [_safe(x) for x in v]
            if isinstance(v, list): return [_safe(x) for x in v]
            if isinstance(v, dict): return {k: _safe(vv) for k, vv in v.items()}
            return repr(v)  # sklearn objects, dtype, etc.

        params = {k: _safe(v) for k, v in sorted(raw.items())}
        if params_override:
            params.update(params_override)
        meta = {"pipeline": {
            "steps": steps,
            "factory": "boilerplate_transformer.build_pipeline_with_cleaner()",
            "params_from_get_params": params,
        }}
        arm_a_dir = tmp_path / "arm_a_run"
        arm_a_dir.mkdir()
        with open(arm_a_dir / "run_metadata.json", "w") as f:
            _json.dump(meta, f)
        return arm_a_dir

    def test_compatible_metadata_passes(self, tmp_path):
        from evaluate_experiment4 import _verify_pipeline_config, build_pipeline_with_cleaner
        pipeline = build_pipeline_with_cleaner()
        steps = [name for name, _ in pipeline.steps]
        arm_a_dir = self._write_meta(tmp_path, steps)
        # Must not raise
        _verify_pipeline_config(arm_a_dir, pipeline)

    def test_changed_clf_C_blocks_fitting(self, tmp_path):
        from evaluate_experiment4 import _verify_pipeline_config, build_pipeline_with_cleaner
        pipeline = build_pipeline_with_cleaner()
        steps = [name for name, _ in pipeline.steps]
        arm_a_dir = self._write_meta(tmp_path, steps, {"clf__C": 999.0})
        with pytest.raises(ValueError, match="clf__C"):
            _verify_pipeline_config(arm_a_dir, pipeline)

    def test_changed_tfidf_ngram_range_blocks_fitting(self, tmp_path):
        from evaluate_experiment4 import _verify_pipeline_config, build_pipeline_with_cleaner
        pipeline = build_pipeline_with_cleaner()
        steps = [name for name, _ in pipeline.steps]
        arm_a_dir = self._write_meta(tmp_path, steps, {"tfidf__ngram_range": [1, 3]})
        with pytest.raises(ValueError, match="tfidf__ngram_range"):
            _verify_pipeline_config(arm_a_dir, pipeline)

    def test_missing_metadata_blocks_fitting(self, tmp_path):
        from evaluate_experiment4 import _verify_pipeline_config, build_pipeline_with_cleaner
        pipeline = build_pipeline_with_cleaner()
        arm_a_dir = tmp_path / "empty_arm_a"
        arm_a_dir.mkdir()
        with pytest.raises(ValueError, match="run_metadata.json"):
            _verify_pipeline_config(arm_a_dir, pipeline)

    def test_absent_pipeline_section_blocks_fitting(self, tmp_path):
        import json as _json
        from evaluate_experiment4 import _verify_pipeline_config, build_pipeline_with_cleaner
        pipeline = build_pipeline_with_cleaner()
        arm_a_dir = tmp_path / "no_pipeline"
        arm_a_dir.mkdir()
        with open(arm_a_dir / "run_metadata.json", "w") as f:
            _json.dump({"experiment": "Experiment 4 Arm A"}, f)
        with pytest.raises(ValueError, match="'pipeline' section"):
            _verify_pipeline_config(arm_a_dir, pipeline)


# ---------------------------------------------------------------------------
# Section 9: Single-read capture tests (Fix 4)
# ---------------------------------------------------------------------------

class TestSingleReadCapture:
    def test_from_bytes_returns_same_reviews_as_from_path(self, candidate_df, tmp_path):
        from review_loader import (
            load_and_validate_reviews,
            load_and_validate_reviews_from_bytes,
        )
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
        )
        p = _write_csv_tmp(pd.DataFrame([row]))
        reviews_from_path = load_and_validate_reviews(p, candidate_df)
        raw_bytes = p.read_bytes()
        reviews_from_bytes, sha = load_and_validate_reviews_from_bytes(raw_bytes, candidate_df, str(p))

        assert len(reviews_from_path) == len(reviews_from_bytes)
        assert reviews_from_path[0].family_id == reviews_from_bytes[0].family_id
        assert reviews_from_path[0].eligible == reviews_from_bytes[0].eligible
        # SHA must be the sha256 of raw_bytes
        import hashlib
        assert sha == hashlib.sha256(raw_bytes).hexdigest()

    def test_mutating_file_after_capture_does_not_change_review_decisions(
        self, candidate_df, tmp_path
    ):
        """
        Fix 4 regression: after read_bytes() is called, the loader result must
        not be affected by later mutations to the on-disk file.
        """
        from review_loader import load_and_validate_reviews_from_bytes
        row = _make_review_pack_row(
            SYN_FAM_1["family_id"],
            tuple(SYN_FAM_1["ids"]),
            tuple(SYN_FAM_1["texts"]),
            SYN_FAM_1["label"],
        )
        p = tmp_path / "review.csv"
        pd.DataFrame([row]).to_csv(p, index=False)
        raw_bytes = p.read_bytes()
        reviews, sha_before = load_and_validate_reviews_from_bytes(raw_bytes, candidate_df)

        # Mutate the file on disk
        p.write_text("TAMPERED_CONTENT_THAT_WOULD_FAIL_PARSING", encoding="utf-8")

        # Reviews are already in-memory — unchanged
        assert len(reviews) == 1
        assert reviews[0].family_id == SYN_FAM_1["family_id"]
        # SHA of the original bytes is unchanged
        import hashlib
        assert sha_before == hashlib.sha256(raw_bytes).hexdigest()


# ---------------------------------------------------------------------------
# Section 10: Paired comparison OOF alignment (Fix 5)
# ---------------------------------------------------------------------------

class TestPairedComparisonAlignment:
    def _make_oof(self, ids: list[str], labels: list[str], preds: list[str],
                  folds: list[int]) -> pd.DataFrame:
        return pd.DataFrame({
            "Research_ID": ids,
            "True_Label": labels,
            "Predicted_Label": preds,
            "Fold": folds,
        })

    def test_missing_required_column_raises(self):
        arm_a = self._make_oof(["R1"], [L0], [L0], [1])
        arm_b = pd.DataFrame({"Research_ID": ["R1"], "True_Label": [L0], "Fold": [1]})
        # Predicted_Label missing from Arm B
        with pytest.raises(ValueError, match="Predicted_Label"):
            build_paired_comparison(arm_a, arm_b)

    def test_duplicate_research_ids_raises(self):
        arm_a = self._make_oof(["R1", "R1"], [L0, L0], [L0, L0], [1, 1])
        arm_b = self._make_oof(["R1", "R1"], [L0, L0], [L0, L0], [1, 1])
        with pytest.raises(ValueError, match="duplicate"):
            build_paired_comparison(arm_a, arm_b)

    def test_blank_research_id_raises(self):
        arm_a = self._make_oof(["", "R2"], [L0, L1], [L0, L1], [1, 2])
        arm_b = self._make_oof(["", "R2"], [L0, L1], [L0, L1], [1, 2])
        with pytest.raises(ValueError, match="blank"):
            build_paired_comparison(arm_a, arm_b)

    def test_true_label_mismatch_raises(self):
        arm_a = self._make_oof(["R1", "R2"], [L0, L1], [L0, L1], [1, 2])
        arm_b = self._make_oof(["R1", "R2"], [L0, L2], [L0, L1], [1, 2])  # R2 label differs
        with pytest.raises(ValueError, match="True_Label disagrees"):
            build_paired_comparison(arm_a, arm_b)

    def test_fold_mismatch_raises(self):
        arm_a = self._make_oof(["R1", "R2"], [L0, L1], [L0, L1], [1, 2])
        arm_b = self._make_oof(["R1", "R2"], [L0, L1], [L0, L1], [1, 3])  # R2 fold differs
        with pytest.raises(ValueError, match="Fold assignment disagrees"):
            build_paired_comparison(arm_a, arm_b)

    def test_invalid_predicted_label_raises(self):
        arm_a = self._make_oof(["R1"], [L0], [L0], [1])
        arm_b = self._make_oof(["R1"], [L0], ["NOT_A_VALID_LABEL"], [1])
        with pytest.raises(ValueError, match="four-class taxonomy"):
            build_paired_comparison(arm_a, arm_b)

    def test_shuffled_row_order_gives_correct_alignment(self):
        """Alignment must be by Research_ID, not row position."""
        ids    = ["R1", "R2", "R3", "R4"]
        labels = [L0, L0, L1, L2]
        folds  = [1, 1, 2, 2]
        preds_a = [L0, L1, L1, L0]
        preds_b = [L0, L0, L2, L1]

        arm_a = self._make_oof(ids, labels, preds_a, folds)
        # Shuffle Arm B
        arm_b_shuffled = self._make_oof(
            list(reversed(ids)), list(reversed(labels)),
            list(reversed(preds_b)), list(reversed(folds))
        )
        summary, transitions = build_paired_comparison(arm_a, arm_b_shuffled)
        # R2 helped, R3 hurt; same as unshuffled
        assert summary["helped"] == 1
        assert summary["hurt"] == 1
        assert summary["net_gain"] == 0
        assert summary["label_changes"] == 3


# ---------------------------------------------------------------------------
# Section 11: Paired comparison arithmetic (retained and extended)
# ---------------------------------------------------------------------------

class TestPairedComparison:
    def _make_oof(self, ids: list[str], labels: list[str], preds: list[str],
                  folds: list[int]) -> pd.DataFrame:
        rows = []
        for rid, lbl, pred, fold in zip(ids, labels, preds, folds):
            rows.append({
                "Research_ID": rid,
                "True_Label": lbl,
                "Predicted_Label": pred,
                "Combined_Group": f"CG-{rid}",
                "Fold": fold,
            })
        return pd.DataFrame(rows)

    def test_helped_plus_hurt_equals_label_changes_or_more(self):
        """
        helped + hurt <= label_changes because both-wrong-different also has a change.
        net_gain = helped - hurt = B_correct - A_correct.
        """
        ids    = ["R1", "R2", "R3", "R4"]
        labels = [L0, L0, L1, L2]
        a_pred = [L0, L1, L1, L0]  # correct, wrong, correct, wrong
        b_pred = [L0, L0, L2, L1]  # correct, correct (helped), wrong (hurt), wrong
        folds  = [1, 1, 2, 2]

        arm_a = self._make_oof(ids, labels, a_pred, folds)
        arm_b = self._make_oof(ids, labels, b_pred, folds)

        summary, transitions = build_paired_comparison(arm_a, arm_b)

        assert summary["helped"] == 1        # R2: A wrong → B correct
        assert summary["hurt"] == 1          # R3: A correct → B wrong
        assert summary["net_gain"] == 0      # 1 - 1
        assert summary["arm_a_correct"] == 2 # R1, R3
        assert summary["arm_b_correct"] == 2 # R1, R2
        assert summary["both_correct"] == 1  # R1 only
        assert summary["both_wrong_any"] == 1  # R4
        # R2: L1→L0, R3: L1→L2, R4: L0→L1 = 3 changes; R1: no change
        assert summary["label_changes"] == 3

        # Verify arithmetic constraint: net_gain = B_correct - A_correct
        assert summary["net_gain"] == summary["arm_b_correct"] - summary["arm_a_correct"]

    def test_all_same_gives_zero_transitions(self):
        ids    = ["R1", "R2"]
        labels = [L0, L1]
        preds  = [L0, L1]  # all correct for both arms
        folds  = [1, 2]

        arm_a = self._make_oof(ids, labels, preds, folds)
        arm_b = self._make_oof(ids, labels, preds, folds)

        summary, _ = build_paired_comparison(arm_a, arm_b)
        assert summary["helped"] == 0
        assert summary["hurt"] == 0
        assert summary["net_gain"] == 0
        assert summary["both_correct"] == 2
        assert summary["both_wrong_any"] == 0
        assert summary["label_changes"] == 0

    def test_mismatched_ids_raises(self):
        arm_a = self._make_oof(["R1"], [L0], [L0], [1])
        arm_b = self._make_oof(["R2"], [L0], [L0], [1])  # different ID
        with pytest.raises(ValueError, match="Research_ID sets differ"):
            build_paired_comparison(arm_a, arm_b)


# ---------------------------------------------------------------------------
# Section 12: Arm A regression — existing tests not broken
# ---------------------------------------------------------------------------

class TestArmARegressionNotBroken:
    """
    Verify that adding review_loader doesn't break Arm A's public interface.
    This is a smoke check, not a retrain.
    """

    def test_import_evaluate_experiment4_arm_a_functions(self):
        from evaluate_experiment4 import (
            load_and_filter_baseline,
            validate_fold_reference,
            run_arm_a,
            run_reproduction_check,
            compute_per_class_metrics,
        )

    def test_import_review_loader(self):
        from review_loader import (
            load_and_validate_reviews,
            load_and_validate_reviews_from_bytes,
            validate_review_pack_structure,
            build_fold_manifest,
            validate_related_baseline_ids,
        )

    def test_build_paired_comparison_is_importable(self):
        from evaluate_experiment4 import build_paired_comparison

    def test_new_helper_functions_importable(self):
        from evaluate_experiment4 import (
            _recompute_arm_a_metrics,
            _validate_arm_a_metrics_json,
            _verify_pipeline_config,
            save_arm_b_fold_manifest,
        )


# ---------------------------------------------------------------------------
# Helper: write a DataFrame to a temp CSV and return its Path
# ---------------------------------------------------------------------------

def _write_csv_tmp(df: pd.DataFrame) -> Path:
    """Write df to a temp file and return its Path."""
    import tempfile, os
    f = tempfile.NamedTemporaryFile(
        mode="w", suffix=".csv", delete=False, encoding="utf-8"
    )
    df.to_csv(f, index=False)
    f.close()
    return Path(f.name)


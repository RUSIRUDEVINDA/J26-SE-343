"""
review_loader.py
----------------
Load and validate the synthetic-family review pack for Experiment 4 Arm B.

This module is responsible for:
  - Structural validation of the review-pack CSV against the candidate dataset
  - Eligibility determination (Approved / Label_Change rules)
  - Per-fold inclusion / exclusion manifest construction
  - All validation errors are raised as ValueError so callers can
    distinguish structural problems from eligibility results

Research context
----------------
The review pack records researcher review decisions for the 100 synthetic
families in the candidate dataset.  Researcher review is not independent
domain-expert governance validation.  Approved families are eligible for
*training* augmentation only; they are never used in evaluation.

Results produced using augmented training data remain development
cross-validation results, not untouched held-out performance.

Assumptions documented here
---------------------------
- Each synthetic family has exactly three variants in the candidate dataset.
  Families that deviate from this are validation errors.
- Related_Baseline_IDs uses a semicolon (';') as delimiter (REVIEW_INSTRUCTIONS.md).
- Variant_Consistency accepted values: Consistent, Minor_variation, Inconsistent.
  No automatic fill for blank Variant_Consistency.
- Missing review rows count as unreviewed, not approved.
- Source-derived records (Research_ID prefix 'SRC-*') are excluded from
  both training and evaluation regardless of review status.

Variant_Consistency semantics
------------------------------
- Consistent: wording differences only; incident meaning and effective
  category are identical across all three variants.
- Minor_variation: slight wording variation; incident meaning and effective
  category remain consistent across variants.
- Inconsistent: variants describe materially different allegations or
  categories; the family requires further review and is not eligible.

Eligibility rules
-----------------
Approved:
  - Review_Decision == 'Approved'
  - Reviewer: nonblank string
  - Review_Date: parseable date string
  - Variant_Consistency in {'Consistent', 'Minor_variation'}
  - Approved_Label: must be absent (blank/NaN) or equal to Proposed_Label
  - Effective label: Proposed_Label (validated against four-class taxonomy)

Label_Change:
  - Review_Decision == 'Label_Change'
  - Reviewer: nonblank string
  - Review_Date: parseable date string
  - Variant_Consistency in {'Consistent', 'Minor_variation'}
  - Approved_Label: required, must be in the four-class taxonomy
  - Review_Notes: required (nonblank)
  - Effective label: Approved_Label (applied in-memory to training rows only)

Excluded without error (not eligible but not a validation problem):
  - Blank / NaN Review_Decision (unreviewed)
  - Rejected_Out_of_Scope / Rejected_Ambiguous / Rejected_Multi_Issue /
    Rejected_Contradictory (reviewer rejected)
  - Needs_Discussion

Validation errors (raise ValueError):
  - Unknown nonblank decision value
  - Incomplete Approved decision (blank reviewer/date/variant_consistency)
  - Incomplete Label_Change decision (blank reviewer/date/Approved_Label/notes)
  - Inconsistent Variant_Consistency with approval
  - Approved_Label for Approved decision that does not match Proposed_Label
  - Approved_Label for Approved decision is from outside the taxonomy
  - Label_Change Approved_Label is outside the taxonomy
"""

from __future__ import annotations

import logging
from datetime import date, datetime
from pathlib import Path
from typing import Any

import pandas as pd

logger = logging.getLogger("review_loader")

# ---------------------------------------------------------------------------
# Vocabulary constants
# ---------------------------------------------------------------------------

# Four-class taxonomy (must stay in sync with config.LABEL_ORDER)
FOUR_CLASS_LABELS: frozenset[str] = frozenset([
    "Administrative / Procedural / Integrity",
    "Lease Revenue / Payment / Enforcement",
    "Unauthorized Allocation / Transfer / Use",
    "Protected / Environmental Lease Misuse",
])

# Accepted Review_Decision values (complete authoritative list from REVIEW_INSTRUCTIONS.md)
ACCEPTED_DECISIONS: frozenset[str] = frozenset([
    "Approved",
    "Label_Change",
    "Rejected_Out_of_Scope",
    "Rejected_Ambiguous",
    "Rejected_Multi_Issue",
    "Rejected_Contradictory",
    "Needs_Discussion",
])

# Decisions that make a family eligible for augmentation
ELIGIBLE_DECISIONS: frozenset[str] = frozenset(["Approved", "Label_Change"])

# Decisions that are explicitly excluded (but not errors)
EXCLUDED_DECISIONS: frozenset[str] = frozenset([
    "Rejected_Out_of_Scope",
    "Rejected_Ambiguous",
    "Rejected_Multi_Issue",
    "Rejected_Contradictory",
    "Needs_Discussion",
])

# Variant_Consistency values that are compatible with approval
APPROVAL_COMPATIBLE_CONSISTENCY: frozenset[str] = frozenset([
    "Consistent",
    "Minor_variation",
])

# All valid Variant_Consistency vocabulary values
VARIANT_CONSISTENCY_VOCABULARY: frozenset[str] = frozenset([
    "Consistent",
    "Minor_variation",
    "Inconsistent",
])

# Delimiter for Related_Baseline_IDs (REVIEW_INSTRUCTIONS.md §5)
RELATED_IDS_DELIMITER: str = ";"

# Required columns in the review pack CSV
REVIEW_PACK_REQUIRED_COLS: frozenset[str] = frozenset([
    "Synthetic_Family_ID",
    "Research_ID_V1",
    "Research_ID_V2",
    "Research_ID_V3",
    "Canonical_English_Text_V1",
    "Canonical_English_Text_V2",
    "Canonical_English_Text_V3",
    "Proposed_Label",
    "Reviewer",
    "Review_Date",
    "Review_Decision",
    "Approved_Label",
    "Variant_Consistency",
    "Related_Baseline_IDs",
    "Review_Notes",
])

# Exact number of variants per family expected in the candidate dataset
EXPECTED_VARIANTS_PER_FAMILY: int = 3


# ---------------------------------------------------------------------------
# Dataclasses / typed dicts
# ---------------------------------------------------------------------------

class FamilyReview:
    """
    Validated review record for a single synthetic family.

    Attributes
    ----------
    family_id : str
    research_ids : list[str]  — [V1, V2, V3] Research_IDs in that order
    proposed_label : str
    review_decision : str | None  — None = unreviewed
    reviewer : str | None
    review_date : str | None
    variant_consistency : str | None
    approved_label_raw : str | None  — raw value from pack (may be blank)
    review_notes : str | None
    related_baseline_ids : list[str]  — parsed, may be empty
    effective_label : str | None  — label to use in training, or None if not eligible
    eligible : bool
    exclusion_reason : str | None  — human-readable if not eligible
    """

    __slots__ = (
        "family_id",
        "research_ids",
        "proposed_label",
        "review_decision",
        "reviewer",
        "review_date",
        "variant_consistency",
        "approved_label_raw",
        "review_notes",
        "related_baseline_ids",
        "effective_label",
        "eligible",
        "exclusion_reason",
    )

    def __init__(
        self,
        *,
        family_id: str,
        research_ids: list[str],
        proposed_label: str,
        review_decision: str | None,
        reviewer: str | None,
        review_date: str | None,
        variant_consistency: str | None,
        approved_label_raw: str | None,
        review_notes: str | None,
        related_baseline_ids: list[str],
        effective_label: str | None,
        eligible: bool,
        exclusion_reason: str | None,
    ) -> None:
        self.family_id = family_id
        self.research_ids = research_ids
        self.proposed_label = proposed_label
        self.review_decision = review_decision
        self.reviewer = reviewer
        self.review_date = review_date
        self.variant_consistency = variant_consistency
        self.approved_label_raw = approved_label_raw
        self.review_notes = review_notes
        self.related_baseline_ids = related_baseline_ids
        self.effective_label = effective_label
        self.eligible = eligible
        self.exclusion_reason = exclusion_reason

    def __repr__(self) -> str:
        return (
            f"FamilyReview(family_id={self.family_id!r}, eligible={self.eligible}, "
            f"decision={self.review_decision!r}, effective_label={self.effective_label!r})"
        )


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------

def _blank(val: Any) -> bool:
    """Return True if val is None, NaN, or a whitespace-only string."""
    if val is None:
        return True
    if isinstance(val, float):
        import math
        return math.isnan(val)
    if isinstance(val, str):
        return val.strip() == ""
    return False


def _nonempty(val: Any) -> str | None:
    """Return stripped string if nonempty, else None."""
    if _blank(val):
        return None
    return str(val).strip()


def _parse_date(val: Any) -> bool:
    """Return True if val is a parseable date/datetime string."""
    s = _nonempty(val)
    if s is None:
        return False
    for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%Y/%m/%d", "%d-%m-%Y"):
        try:
            datetime.strptime(s, fmt)
            return True
        except ValueError:
            continue
    return False


def _parse_related_ids(val: Any) -> list[str]:
    """
    Parse Related_Baseline_IDs field using ';' delimiter.

    Returns a list of stripped nonblank IDs, or an empty list if blank.
    Note: absence of recorded IDs does not confirm independence (per instructions).
    """
    s = _nonempty(val)
    if s is None:
        return []
    parts = [p.strip() for p in s.split(RELATED_IDS_DELIMITER)]
    return [p for p in parts if p]


# ---------------------------------------------------------------------------
# Structural validation of the review pack
# ---------------------------------------------------------------------------

def validate_review_pack_structure(
    review_df: pd.DataFrame,
    candidate_df: pd.DataFrame,
) -> None:
    """
    Validate the review pack CSV structure against the candidate dataset.

    Raises ValueError describing all problems found.

    Checks
    ------
    1. Required columns are present.
    2. Synthetic_Family_IDs are unique and nonblank in the pack.
    3. All pack family IDs exist in the candidate dataset's synthetic rows.
    4. No unknown family IDs (in candidate but not in pack) are silently dropped —
       families not in the pack are treated as unreviewed (warning only).
    5. For each family, the three Research_IDs in the pack exist in the candidate
       dataset as synthetic variants of that family.
    6. Pack variant texts match the candidate dataset texts (tamper check).
    7. Proposed_Label matches the candidate dataset's ML_Label_4Class for the family.
    8. Each family has exactly three distinct Research_IDs in the candidate dataset.
    """
    errors: list[str] = []

    # 1. Required columns
    missing_cols = REVIEW_PACK_REQUIRED_COLS - set(review_df.columns)
    if missing_cols:
        errors.append(f"Review pack missing required columns: {sorted(missing_cols)}")

    if errors:
        # Cannot proceed without required columns
        raise ValueError("Review pack structural errors:\n" + "\n".join(f"  - {e}" for e in errors))

    # 2. Unique nonblank family IDs
    blank_fam = review_df["Synthetic_Family_ID"].isna().sum() + (
        review_df["Synthetic_Family_ID"].fillna("").str.strip() == ""
    ).sum()
    if blank_fam > 0:
        errors.append(f"{blank_fam} blank/null Synthetic_Family_ID value(s) in review pack.")
    dup_fam = review_df["Synthetic_Family_ID"].duplicated().sum()
    if dup_fam > 0:
        errors.append(f"{dup_fam} duplicate Synthetic_Family_ID value(s) in review pack.")

    # Isolate synthetic records from candidate
    SYNTHETIC_ORIGIN = "New synthetic scenario"
    if "Record_Origin" not in candidate_df.columns:
        errors.append("Candidate dataset missing 'Record_Origin' column.")
    if "Synthetic_Family_ID" not in candidate_df.columns:
        errors.append("Candidate dataset missing 'Synthetic_Family_ID' column.")

    if errors:
        raise ValueError("Review pack structural errors:\n" + "\n".join(f"  - {e}" for e in errors))

    synthetic_df = candidate_df[candidate_df["Record_Origin"] == SYNTHETIC_ORIGIN].copy()
    candidate_family_ids = set(synthetic_df["Synthetic_Family_ID"].dropna().unique().tolist())
    pack_family_ids = set(review_df["Synthetic_Family_ID"].dropna().str.strip().unique().tolist())

    # 3. Unknown family IDs in pack
    unknown_in_pack = pack_family_ids - candidate_family_ids
    if unknown_in_pack:
        errors.append(
            f"{len(unknown_in_pack)} family ID(s) in review pack not found in candidate dataset: "
            f"{sorted(unknown_in_pack)[:5]}"
        )

    # 4. Families in candidate but not in pack → unreviewed (log only, not an error)
    unreviewed = candidate_family_ids - pack_family_ids
    if unreviewed:
        logger.warning(
            "%d synthetic family/families in the candidate dataset have no review-pack row "
            "(treated as unreviewed): %s ...",
            len(unreviewed),
            sorted(unreviewed)[:5],
        )

    if errors:
        raise ValueError("Review pack structural errors:\n" + "\n".join(f"  - {e}" for e in errors))

    # Per-family structural checks
    # Build lookup: family_id → list of (Research_ID, Canonical_English_Text, ML_Label_4Class, Synthetic_Variant)
    family_records: dict[str, list[dict[str, Any]]] = {}
    for _, row in synthetic_df.iterrows():
        fid = str(row["Synthetic_Family_ID"]).strip()
        family_records.setdefault(fid, []).append({
            "Research_ID": row["Research_ID"],
            "Text": row["Canonical_English_Text"],
            "Label": row["ML_Label_4Class"],
            "Variant": row.get("Synthetic_Variant"),
        })

    for _, pack_row in review_df.iterrows():
        fid = str(pack_row["Synthetic_Family_ID"]).strip()
        if fid not in candidate_family_ids:
            continue  # Already reported above

        records = family_records.get(fid, [])

        # 8. Exactly three distinct Research_IDs in candidate
        candidate_ids = sorted(set(r["Research_ID"] for r in records))
        if len(candidate_ids) != EXPECTED_VARIANTS_PER_FAMILY:
            errors.append(
                f"Family {fid}: expected {EXPECTED_VARIANTS_PER_FAMILY} variants in candidate "
                f"dataset, found {len(candidate_ids)}: {candidate_ids}"
            )
            continue

        # 5. Pack Research_IDs exist in candidate and belong to this family
        pack_ids = [
            _nonempty(pack_row.get("Research_ID_V1")),
            _nonempty(pack_row.get("Research_ID_V2")),
            _nonempty(pack_row.get("Research_ID_V3")),
        ]
        if any(pid is None for pid in pack_ids):
            errors.append(f"Family {fid}: blank Research_ID_V1/V2/V3 in review pack.")
            continue
        pack_id_set = set(pack_ids)
        candidate_id_set = set(candidate_ids)
        if pack_id_set != candidate_id_set:
            errors.append(
                f"Family {fid}: review pack Research_IDs {sorted(pack_id_set)} "
                f"do not match candidate dataset IDs {sorted(candidate_id_set)}."
            )

        # 6. Variant texts match (tamper check)
        candidate_text_map = {r["Research_ID"]: r["Text"] for r in records}
        for vcol, vid in [
            ("Canonical_English_Text_V1", pack_ids[0]),
            ("Canonical_English_Text_V2", pack_ids[1]),
            ("Canonical_English_Text_V3", pack_ids[2]),
        ]:
            if vid not in candidate_text_map:
                continue  # already reported above
            pack_text = _nonempty(pack_row.get(vcol))
            cand_text = str(candidate_text_map[vid]).strip() if candidate_text_map.get(vid) else None
            if pack_text is None or cand_text is None:
                errors.append(f"Family {fid} {vcol}: blank text in review pack or candidate.")
            elif pack_text != cand_text:
                errors.append(
                    f"Family {fid} {vid}: review pack text does not match candidate dataset "
                    f"(first diff at char {next((i for i,(a,b) in enumerate(zip(pack_text,cand_text)) if a!=b), min(len(pack_text),len(cand_text)))})."
                )

        # 7. Proposed_Label matches candidate label
        candidate_labels = set(r["Label"] for r in records)
        if len(candidate_labels) != 1:
            errors.append(
                f"Family {fid}: variants have inconsistent labels in candidate dataset: "
                f"{candidate_labels}"
            )
        else:
            candidate_label = next(iter(candidate_labels))
            pack_proposed = _nonempty(pack_row.get("Proposed_Label"))
            if pack_proposed is None:
                errors.append(f"Family {fid}: blank Proposed_Label in review pack.")
            elif pack_proposed != candidate_label:
                errors.append(
                    f"Family {fid}: review pack Proposed_Label={pack_proposed!r} "
                    f"does not match candidate ML_Label_4Class={candidate_label!r}."
                )

    if errors:
        raise ValueError(
            f"Review pack structural errors ({len(errors)} total):\n"
            + "\n".join(f"  - {e}" for e in errors)
        )


# ---------------------------------------------------------------------------
# Eligibility determination per family
# ---------------------------------------------------------------------------

def _determine_eligibility(pack_row: pd.Series) -> tuple[bool, str | None, str | None]:
    """
    Determine eligibility for one pack row.

    Returns (eligible, effective_label, exclusion_reason).

    Raises ValueError for unknown nonblank decision values or incomplete approvals.
    """
    fid = str(pack_row["Synthetic_Family_ID"]).strip()
    decision = _nonempty(pack_row.get("Review_Decision"))
    proposed = _nonempty(pack_row.get("Proposed_Label"))
    approved_label_raw = _nonempty(pack_row.get("Approved_Label"))
    reviewer = _nonempty(pack_row.get("Reviewer"))
    review_date = _nonempty(pack_row.get("Review_Date"))
    consistency = _nonempty(pack_row.get("Variant_Consistency"))
    notes = _nonempty(pack_row.get("Review_Notes"))

    # Unreviewed
    if decision is None:
        return False, None, "unreviewed"

    # Unknown decision
    if decision not in ACCEPTED_DECISIONS:
        raise ValueError(
            f"Family {fid}: unknown Review_Decision value {decision!r}. "
            f"Accepted values: {sorted(ACCEPTED_DECISIONS)}"
        )

    # Excluded non-approval decisions
    if decision in EXCLUDED_DECISIONS:
        return False, None, f"decision={decision}"

    # --- Approved ---
    if decision == "Approved":
        validation_errors: list[str] = []

        if reviewer is None:
            validation_errors.append("Reviewer is blank")
        if not _parse_date(review_date):
            validation_errors.append(f"Review_Date is invalid or blank ({review_date!r})")
        if consistency is None:
            validation_errors.append("Variant_Consistency is blank")
        elif consistency not in VARIANT_CONSISTENCY_VOCABULARY:
            validation_errors.append(
                f"Variant_Consistency={consistency!r} is not in valid vocabulary "
                f"{sorted(VARIANT_CONSISTENCY_VOCABULARY)}"
            )
        elif consistency not in APPROVAL_COMPATIBLE_CONSISTENCY:
            validation_errors.append(
                f"Variant_Consistency={consistency!r} is incompatible with Approved decision "
                f"(must be one of {sorted(APPROVAL_COMPATIBLE_CONSISTENCY)})"
            )
        if proposed is None:
            validation_errors.append("Proposed_Label is blank")
        elif proposed not in FOUR_CLASS_LABELS:
            validation_errors.append(
                f"Proposed_Label={proposed!r} is not in the four-class taxonomy"
            )
        if approved_label_raw is not None:
            # If Approved_Label is supplied for Approved decision, it must match Proposed_Label
            if approved_label_raw not in FOUR_CLASS_LABELS:
                validation_errors.append(
                    f"Approved_Label={approved_label_raw!r} is not in the four-class taxonomy"
                )
            elif approved_label_raw != proposed:
                validation_errors.append(
                    f"Approved_Label={approved_label_raw!r} does not match "
                    f"Proposed_Label={proposed!r} for an Approved decision. "
                    "Use Label_Change decision if the label should differ."
                )

        if validation_errors:
            raise ValueError(
                f"Family {fid}: incomplete Approved decision — "
                + "; ".join(validation_errors)
            )

        return True, proposed, None

    # --- Label_Change ---
    if decision == "Label_Change":
        validation_errors = []

        if reviewer is None:
            validation_errors.append("Reviewer is blank")
        if not _parse_date(review_date):
            validation_errors.append(f"Review_Date is invalid or blank ({review_date!r})")
        if consistency is None:
            validation_errors.append("Variant_Consistency is blank")
        elif consistency not in VARIANT_CONSISTENCY_VOCABULARY:
            validation_errors.append(
                f"Variant_Consistency={consistency!r} is not in valid vocabulary "
                f"{sorted(VARIANT_CONSISTENCY_VOCABULARY)}"
            )
        elif consistency not in APPROVAL_COMPATIBLE_CONSISTENCY:
            validation_errors.append(
                f"Variant_Consistency={consistency!r} is incompatible with Label_Change decision "
                f"(must be one of {sorted(APPROVAL_COMPATIBLE_CONSISTENCY)})"
            )
        if approved_label_raw is None:
            validation_errors.append("Approved_Label is required for Label_Change")
        elif approved_label_raw not in FOUR_CLASS_LABELS:
            validation_errors.append(
                f"Approved_Label={approved_label_raw!r} is not in the four-class taxonomy"
            )
        if notes is None:
            validation_errors.append("Review_Notes is required for Label_Change")

        if validation_errors:
            raise ValueError(
                f"Family {fid}: incomplete Label_Change decision — "
                + "; ".join(validation_errors)
            )

        return True, approved_label_raw, None

    # Should not reach here given ACCEPTED_DECISIONS is exhaustive
    raise ValueError(f"Family {fid}: unhandled decision {decision!r}")


# ---------------------------------------------------------------------------
# Main public API
# ---------------------------------------------------------------------------

def load_and_validate_reviews(
    review_path: Path,
    candidate_df: pd.DataFrame,
) -> list[FamilyReview]:
    """
    Load the review pack CSV from disk, validate its structure against the
    candidate dataset, and return a list of FamilyReview objects.

    The file is read once.  Use load_and_validate_reviews_from_bytes() when
    a single captured byte sequence must be used for hashing, parsing,
    validation and snapshot saving.

    Raises
    ------
    FileNotFoundError : review_path does not exist
    ValueError : any structural or eligibility validation error

    Notes
    -----
    Source datasets and review files are never modified by this function.
    Missing review rows count as unreviewed, not approved.
    Researcher review is not independent domain-expert governance validation.
    Results remain development cross-validation, not held-out performance.
    """
    if not review_path.exists():
        raise FileNotFoundError(
            f"Review pack not found: {review_path}. "
            "This file is required to run Arm B."
        )

    raw_bytes = review_path.read_bytes()
    reviews, _ = _parse_and_validate_reviews(raw_bytes, candidate_df, str(review_path))
    return reviews


def load_and_validate_reviews_from_bytes(
    raw_bytes: bytes,
    candidate_df: pd.DataFrame,
    source_description: str = "<bytes>",
) -> tuple[list[FamilyReview], str]:
    """
    Validate a review pack from an already-captured byte sequence.

    One call performs SHA-256 hashing, CSV parsing, structural validation
    and eligibility determination — all on the same bytes.  The caller
    saves the bytes directly as review_snapshot, ensuring the snapshot
    matches what was validated.

    Parameters
    ----------
    raw_bytes : bytes
        Complete byte content of the review pack CSV.
    candidate_df : pd.DataFrame
        Candidate dataset used for structural validation.
    source_description : str
        Human-readable source label for log and error messages.

    Returns
    -------
    (reviews, sha256_hex)
        reviews      : list[FamilyReview]
        sha256_hex   : hex digest of raw_bytes

    Raises
    ------
    ValueError : any structural or eligibility validation error

    Notes
    -----
    Researcher review is not independent domain-expert governance validation.
    Results remain development cross-validation, not held-out performance.
    """
    import hashlib
    sha256_hex = hashlib.sha256(raw_bytes).hexdigest()
    reviews, _ = _parse_and_validate_reviews(raw_bytes, candidate_df, source_description)
    return reviews, sha256_hex


def _parse_and_validate_reviews(
    raw_bytes: bytes,
    candidate_df: pd.DataFrame,
    source_description: str,
) -> tuple[list[FamilyReview], pd.DataFrame]:
    """
    Internal: parse CSV bytes, validate and return (reviews, review_df).
    review_df is returned so callers can save the snapshot if needed.
    """
    import io
    review_df = pd.read_csv(io.BytesIO(raw_bytes))
    logger.info(
        "Loaded review pack: %d row(s), %d column(s) from '%s'",
        len(review_df), len(review_df.columns), source_description,
    )

    # Structural validation (raises ValueError on failure)
    validate_review_pack_structure(review_df, candidate_df)
    logger.info("Review pack structural validation passed.")

    # Build lookup for candidate variant texts/labels
    SYNTHETIC_ORIGIN = "New synthetic scenario"
    synthetic_df = candidate_df[candidate_df["Record_Origin"] == SYNTHETIC_ORIGIN]
    family_records: dict[str, list[dict[str, Any]]] = {}
    for _, row in synthetic_df.iterrows():
        fid = str(row["Synthetic_Family_ID"]).strip()
        family_records.setdefault(fid, []).append({
            "Research_ID": row["Research_ID"],
            "Text": row["Canonical_English_Text"],
            "Label": row["ML_Label_4Class"],
        })

    reviews: list[FamilyReview] = []
    validation_errors: list[str] = []

    for _, pack_row in review_df.iterrows():
        fid = str(pack_row["Synthetic_Family_ID"]).strip()
        pack_ids = [
            _nonempty(pack_row.get("Research_ID_V1")),
            _nonempty(pack_row.get("Research_ID_V2")),
            _nonempty(pack_row.get("Research_ID_V3")),
        ]
        proposed = _nonempty(pack_row.get("Proposed_Label"))
        related_raw = pack_row.get("Related_Baseline_IDs")

        try:
            eligible, effective_label, exclusion_reason = _determine_eligibility(pack_row)
        except ValueError as exc:
            validation_errors.append(str(exc))
            continue

        related_ids = _parse_related_ids(related_raw)

        reviews.append(FamilyReview(
            family_id=fid,
            research_ids=[pid for pid in pack_ids if pid is not None],
            proposed_label=proposed or "",
            review_decision=_nonempty(pack_row.get("Review_Decision")),
            reviewer=_nonempty(pack_row.get("Reviewer")),
            review_date=_nonempty(pack_row.get("Review_Date")),
            variant_consistency=_nonempty(pack_row.get("Variant_Consistency")),
            approved_label_raw=_nonempty(pack_row.get("Approved_Label")),
            review_notes=_nonempty(pack_row.get("Review_Notes")),
            related_baseline_ids=related_ids,
            effective_label=effective_label,
            eligible=eligible,
            exclusion_reason=exclusion_reason,
        ))

    if validation_errors:
        raise ValueError(
            f"Review eligibility validation errors ({len(validation_errors)} total):\n"
            + "\n".join(f"  - {e}" for e in validation_errors)
        )

    n_eligible = sum(1 for r in reviews if r.eligible)
    n_unreviewed = sum(1 for r in reviews if r.review_decision is None)
    n_rejected = sum(
        1 for r in reviews
        if r.review_decision is not None and not r.eligible
    )
    logger.info(
        "Review eligibility: %d eligible, %d unreviewed, %d rejected/excluded "
        "(out of %d total review rows)",
        n_eligible, n_unreviewed, n_rejected, len(reviews),
    )

    return reviews, review_df



def validate_related_baseline_ids(
    reviews: list[FamilyReview],
    baseline_ids: set[str],
) -> None:
    """
    Validate that every Related_Baseline_ID recorded by reviewers exists
    in the baseline dataset.

    Raises ValueError listing all unknown IDs.
    Blank Related_Baseline_IDs are skipped (not an error).
    """
    errors: list[str] = []
    for rev in reviews:
        unknown = [rid for rid in rev.related_baseline_ids if rid not in baseline_ids]
        if unknown:
            errors.append(
                f"Family {rev.family_id}: Related_Baseline_IDs contain unknown "
                f"baseline Research_IDs: {unknown}"
            )
    if errors:
        raise ValueError(
            f"Unknown Related_Baseline_IDs ({len(errors)} family/families):\n"
            + "\n".join(f"  - {e}" for e in errors)
        )


def build_fold_manifest(
    reviews: list[FamilyReview],
    baseline_df: pd.DataFrame,
    fold_map: dict[str, int],
    cg_map: dict[str, str],
    id_column: str = "Research_ID",
) -> list[dict[str, Any]]:
    """
    Build the per-fold inclusion/exclusion manifest for Arm B augmentation.

    For each eligible family:
      - Determine which baseline evaluation Combined_Groups are in each fold
      - Map Related_Baseline_IDs to their Combined_Groups
      - Exclude the entire family from fold F if any related baseline record
        belongs to fold F's evaluation set

    All families are recorded, including unreviewed and rejected ones.

    Returns
    -------
    list of dicts, one per (family, fold) combination for eligible families,
    plus one row per non-eligible family (fold-independent).

    Notes
    -----
    Blank Related_Baseline_IDs do not prove independence; the absence of a
    recorded relationship does not confirm that a synthetic family is unrelated
    to evaluation records.

    The source dataset and review files are never modified by this function.
    """
    # Map baseline IDs → Combined_Group and Fold
    baseline_id_set = set(baseline_df[id_column].tolist())
    id_to_cg = {rid: cg_map[rid] for rid in baseline_id_set if rid in cg_map}
    id_to_fold = {rid: fold_map[rid] for rid in baseline_id_set if rid in fold_map}

    # Map CombinedGroup → evaluation fold
    cg_to_eval_fold: dict[str, int] = {}
    for rid, cg in id_to_cg.items():
        f = id_to_fold[rid]
        cg_to_eval_fold[cg] = f

    unique_folds = sorted(set(fold_map.values()))
    manifest_rows: list[dict[str, Any]] = []

    for rev in reviews:
        # Resolve related CombinedGroups and their evaluation folds
        related_cgs: list[str] = []
        related_folds: list[int] = []
        for rid in rev.related_baseline_ids:
            if rid in id_to_cg:
                cg = id_to_cg[rid]
                related_cgs.append(cg)
                ef = cg_to_eval_fold.get(cg)
                if ef is not None and ef not in related_folds:
                    related_folds.append(ef)

        related_cgs_str = "; ".join(sorted(set(related_cgs))) or ""
        related_folds_str = "; ".join(str(f) for f in sorted(set(related_folds))) or ""

        if not rev.eligible:
            manifest_rows.append({
                "Synthetic_Family_ID": rev.family_id,
                "Research_IDs": "; ".join(rev.research_ids),
                "Review_Decision": rev.review_decision or "unreviewed",
                "Effective_Label": rev.effective_label or "",
                "Fold": "all",
                "Included": False,
                "Exclusion_Reason": rev.exclusion_reason or "not eligible",
                "Related_Baseline_IDs": RELATED_IDS_DELIMITER.join(rev.related_baseline_ids),
                "Related_Combined_Groups": related_cgs_str,
                "Related_Evaluation_Folds": related_folds_str,
            })
        else:
            for fold_idx in unique_folds:
                # Exclude if any related baseline record is evaluated in this fold
                excluded_by_relation = fold_idx in related_folds
                manifest_rows.append({
                    "Synthetic_Family_ID": rev.family_id,
                    "Research_IDs": "; ".join(rev.research_ids),
                    "Review_Decision": rev.review_decision or "",
                    "Effective_Label": rev.effective_label or "",
                    "Fold": fold_idx,
                    "Included": not excluded_by_relation,
                    "Exclusion_Reason": (
                        f"related baseline in eval fold {fold_idx}"
                        if excluded_by_relation else ""
                    ),
                    "Related_Baseline_IDs": RELATED_IDS_DELIMITER.join(rev.related_baseline_ids),
                    "Related_Combined_Groups": related_cgs_str,
                    "Related_Evaluation_Folds": related_folds_str,
                })

    return manifest_rows

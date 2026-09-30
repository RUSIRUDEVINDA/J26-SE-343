"""
data_loader.py
--------------
Reusable data-loading and validation functions for the Component 4
GovernanceIntelligence text-classification ML module.

Validation contract
-------------------
* Required columns must exist.
* Dataset must not be empty.
* Complaint text (Canonical_English_Text) must not be null or blank.
* Target labels (ML_Label_4Class) must not be null.
* All target values must belong to the fixed four-class taxonomy.
* Source_Group_ID must exist and must not be null.
* Duplicate complaint texts are reported as a data-quality warning,
  but are NOT silently dropped (removal is a separate, explicit step).
* Invalid labels raise ValueError; they are never silently coerced.

Privacy boundary
----------------
This module does not log raw complaint text or record-level identifiers
beyond what is necessary for error messages during development.
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Optional

import pandas as pd

import sys
import os
sys.path.insert(0, str(Path(__file__).resolve().parent))
from config import (
    DATASET_PATH,
    TEXT_COLUMN,
    TARGET_COLUMN,
    GROUP_COLUMN,
    ID_COLUMN,
    LABEL_ORDER,
)

logger = logging.getLogger(__name__)

REQUIRED_COLUMNS = [TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN, ID_COLUMN]
ALLOWED_LABELS   = set(LABEL_ORDER)


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------

def _assert_columns_present(df: pd.DataFrame, path: Path) -> None:
    missing = [c for c in REQUIRED_COLUMNS if c not in df.columns]
    if missing:
        raise ValueError(
            f"Dataset '{path}' is missing required columns: {missing}. "
            f"Available columns: {list(df.columns)}"
        )


def _assert_not_empty(df: pd.DataFrame, path: Path) -> None:
    if len(df) == 0:
        raise ValueError(f"Dataset '{path}' contains no rows.")


def _assert_no_null_text(df: pd.DataFrame) -> None:
    nulls = df[TEXT_COLUMN].isna().sum()
    if nulls > 0:
        raise ValueError(
            f"Column '{TEXT_COLUMN}' has {nulls} null value(s). "
            "All records must have complaint text."
        )


def _assert_no_blank_text(df: pd.DataFrame) -> None:
    blanks = (df[TEXT_COLUMN].str.strip() == "").sum()
    if blanks > 0:
        raise ValueError(
            f"Column '{TEXT_COLUMN}' has {blanks} blank/empty value(s). "
            "All records must have non-empty complaint text."
        )


def _assert_no_null_labels(df: pd.DataFrame) -> None:
    nulls = df[TARGET_COLUMN].isna().sum()
    if nulls > 0:
        raise ValueError(
            f"Column '{TARGET_COLUMN}' has {nulls} null value(s). "
            "All records must have a target label."
        )


def _assert_valid_labels(df: pd.DataFrame) -> None:
    actual_labels = set(df[TARGET_COLUMN].unique())
    unexpected    = actual_labels - ALLOWED_LABELS
    if unexpected:
        raise ValueError(
            f"Column '{TARGET_COLUMN}' contains unexpected label(s): {unexpected}. "
            f"Allowed labels: {ALLOWED_LABELS}"
        )


def _assert_no_null_groups(df: pd.DataFrame) -> None:
    nulls = df[GROUP_COLUMN].isna().sum()
    if nulls > 0:
        raise ValueError(
            f"Column '{GROUP_COLUMN}' has {nulls} null value(s). "
            "Source_Group_ID must be present for all records (used for leakage-safe CV)."
        )


def _warn_duplicate_text(df: pd.DataFrame) -> None:
    dup_mask  = df[TEXT_COLUMN].duplicated(keep=False)
    dup_count = dup_mask.sum()
    if dup_count > 0:
        logger.warning(
            "DATA QUALITY WARNING: %d record(s) share identical Canonical_English_Text. "
            "Duplicates are NOT automatically removed. "
            "Review the dataset before training.",
            dup_count,
        )


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def load_and_validate(path: Optional[Path] = None) -> pd.DataFrame:
    """
    Load the dataset from *path* (defaults to config.DATASET_PATH) and
    run all validation checks.

    Returns
    -------
    pd.DataFrame
        The validated dataset.

    Raises
    ------
    FileNotFoundError
        If the file does not exist.
    ValueError
        If any mandatory validation check fails.
    """
    if path is None:
        path = DATASET_PATH

    path = Path(path)
    if not path.exists():
        raise FileNotFoundError(
            f"Dataset not found: '{path}'. "
            "Ensure the CSV is present before running evaluation or training."
        )

    logger.info("Loading dataset from '%s'", path)
    df = pd.read_csv(path)

    _assert_columns_present(df, path)
    _assert_not_empty(df, path)
    _assert_no_null_text(df)
    _assert_no_blank_text(df)
    _assert_no_null_labels(df)
    _assert_valid_labels(df)
    _assert_no_null_groups(df)
    _warn_duplicate_text(df)

    logger.info(
        "Dataset validated: %d rows, %d unique labels, %d unique groups.",
        len(df),
        df[TARGET_COLUMN].nunique(),
        df[GROUP_COLUMN].nunique(),
    )
    return df


def get_class_distribution(df: pd.DataFrame) -> dict[str, int]:
    """
    Return a dict mapping each label to its count, in LABEL_ORDER order.
    """
    counts = df[TARGET_COLUMN].value_counts()
    return {label: int(counts.get(label, 0)) for label in LABEL_ORDER}


def get_feature_arrays(df: pd.DataFrame):
    """
    Extract (X, y, groups) from a validated DataFrame.

    Returns
    -------
    X : list[str]
        Complaint texts.
    y : list[str]
        Target labels.
    groups : list[str]
        Source group IDs (for StratifiedGroupKFold).
    """
    X      = df[TEXT_COLUMN].tolist()
    y      = df[TARGET_COLUMN].tolist()
    groups = df[GROUP_COLUMN].tolist()
    return X, y, groups

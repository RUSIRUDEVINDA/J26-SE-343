"""
boilerplate_transformer.py
--------------------------
Stateless sklearn-compatible text transformer that removes specific
research boilerplate phrases from governance complaint narratives.

Purpose
-------
Removes exactly the listed audit/research metadata phrases from each
record text before the TF-IDF vectorisation step in Experiment 3.

This is a deterministic preprocessing step. The transformer:
- Removes ONLY the three exact listed phrases via literal string replacement.
- Preserves all incident-specific content: location names, issue descriptions,
  amounts, dates, units, permissions, approvals, environmental context,
  arrears, transfers, and lease obligations.
- Collapses repeated whitespace and trims the result.
- Is stateless: fit() does nothing; transform() is pure.
- Is idempotent: applying it twice produces the same result as once.

The transformer may be inserted before TfidfVectorizer inside a Pipeline.
It does NOT tokenize, lowercase, or alter vocabulary—those remain the
responsibility of TF-IDF.

Research context
----------------
This transformer is used in Experiment 3 of the Component 4
GovernanceIntelligence ML module. It must not be used in Experiment 1
(baseline) or Experiment 2 evaluations.

The baseline pipeline factory (model.build_pipeline) is unchanged.
Experiment 3 wraps it with an additional first step via build_pipeline_with_cleaner().
"""

from __future__ import annotations

import re
from typing import Union

import numpy as np
from sklearn.base import BaseEstimator, TransformerMixin
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from config import TFIDF_CONFIG, LR_CONFIG


# ---------------------------------------------------------------------------
# Exact boilerplate phrases permitted for removal — do NOT extend this list
# based on experimental results.
# ---------------------------------------------------------------------------

BOILERPLATE_PHRASES: tuple[str, ...] = (
    "was identified in official audit/report material as a government/state land lease case involving",
    "The Land Commissioner has confirmed that this is a genuine, direct, distinct state-land lease incident.",
    "File-level details should be retained from the Commissioner's records when available.",
)


def strip_research_boilerplate(text: str) -> str:
    """
    Remove exact listed boilerplate phrases from a single text string.

    Parameters
    ----------
    text : str
        Input governance complaint text.

    Returns
    -------
    str
        Text with boilerplate phrases removed and whitespace collapsed.
        Original text is returned (whitespace-normalized) if no phrase matches.
    """
    result = text
    for phrase in BOILERPLATE_PHRASES:
        result = result.replace(phrase, " ")
    # Collapse contiguous whitespace (spaces, tabs, newlines) and trim.
    result = re.sub(r"\s+", " ", result).strip()
    return result


class BoilerplateStripper(BaseEstimator, TransformerMixin):
    """
    Stateless sklearn transformer that removes research boilerplate phrases.

    Designed to be placed as the first step inside an sklearn Pipeline,
    before TfidfVectorizer.

    Parameters
    ----------
    None

    Notes
    -----
    - fit() is a no-op (no state to learn from training data).
    - transform() applies strip_research_boilerplate() to each element.
    - Idempotent: applying twice gives the same result as once.
    - Does NOT modify the original dataset or any DataFrame.
    """

    def fit(self, X: Union[list[str], np.ndarray], y=None) -> "BoilerplateStripper":
        """No-op. Returns self to allow chaining."""
        return self

    def transform(self, X: Union[list[str], np.ndarray]) -> list[str]:
        """
        Apply boilerplate removal to each text in X.

        Parameters
        ----------
        X : list[str] or np.ndarray of str
            Input texts.

        Returns
        -------
        list[str]
            Texts with exact boilerplate phrases removed.
        """
        return [strip_research_boilerplate(str(text)) for text in X]


def build_pipeline_with_cleaner() -> Pipeline:
    """
    Return a fresh, unfitted pipeline with boilerplate stripping before TF-IDF.

    This is the Experiment 3 pipeline. The baseline build_pipeline() in model.py
    is NOT modified.

    Pipeline steps:
        'boilerplate'  — BoilerplateStripper (stateless, exact literal removal)
        'tfidf'        — TfidfVectorizer (same settings as baseline)
        'clf'          — LogisticRegression (same settings as baseline)

    Returns
    -------
    sklearn.pipeline.Pipeline
        Unfitted pipeline ready for use in a single CV fold.
    """
    stripper = BoilerplateStripper()

    vectoriser = TfidfVectorizer(
        lowercase   =TFIDF_CONFIG["lowercase"],
        stop_words  =TFIDF_CONFIG["stop_words"],
        ngram_range =TFIDF_CONFIG["ngram_range"],
        sublinear_tf=TFIDF_CONFIG["sublinear_tf"],
    )

    classifier = LogisticRegression(
        max_iter    =LR_CONFIG["max_iter"],
        class_weight=LR_CONFIG["class_weight"],
        random_state=LR_CONFIG["random_state"],
        C           =LR_CONFIG["C"],
        penalty     =LR_CONFIG["penalty"],
        solver      =LR_CONFIG["solver"],
    )

    return Pipeline(
        steps=[
            ("boilerplate", stripper),
            ("tfidf", vectoriser),
            ("clf", classifier),
        ]
    )

"""
model.py
--------
Pipeline factory for the Component 4 GovernanceIntelligence text classifier.

Architecture (baseline, frozen)
--------------------------------
    English complaint text
            ↓
        TF-IDF  (unigrams + bigrams, sublinear_tf, English stop-words)
            ↓
    Logistic Regression  (L2, C=1.0, class_weight='balanced', max_iter=2000)
            ↓
    4-class governance prediction

Rules
-----
* build_pipeline() must be called once per CV fold.
* The returned Pipeline is always unfitted.
* TF-IDF must be fitted only on the training portion of each fold.
* No pretrained classifier, pretrained vectoriser, embeddings, or LLM is used.

This classifier was built from the project dataset only.
No pretrained language model or pretrained classifier was used.
"""

from __future__ import annotations

from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from config import TFIDF_CONFIG, LR_CONFIG


def build_pipeline() -> Pipeline:
    """
    Return a fresh, unfitted TF-IDF + Logistic Regression pipeline.

    Call this function once per cross-validation fold.
    Never reuse a fitted instance across folds.

    Returns
    -------
    sklearn.pipeline.Pipeline
        A pipeline with steps:
            'tfidf'  — TfidfVectorizer (configured from config.TFIDF_CONFIG)
            'clf'    — LogisticRegression (configured from config.LR_CONFIG)
    """
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

    return Pipeline(steps=[("tfidf", vectoriser), ("clf", classifier)])

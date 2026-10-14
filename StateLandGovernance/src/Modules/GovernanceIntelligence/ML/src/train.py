"""
train.py
--------
Full-dataset training script for the Component 4 GovernanceIntelligence
text-classification ML module.

This script is separate from evaluation.

Execution order
---------------
1. Load and validate the complete current dataset.
2. Build a fresh TF-IDF + Logistic Regression pipeline.
3. Train on all 200 records.
4. Save the pipeline artifact to ML/models/governance_classifier.joblib
5. Save model metadata to ML/models/model_metadata.json

Important
---------
Run evaluate.py first to understand model performance before training.
The final model is trained on ALL available data and is not suitable
for evaluating accuracy by itself (no held-out set is used here).

This classifier was trained from the project dataset only.
No pretrained language model or pretrained classifier was used.

Usage
-----
    python src/train.py          (from ML/ directory)
"""

from __future__ import annotations

import json
import logging
import sys
from datetime import datetime, timezone
from pathlib import Path

import joblib

# Add src/ to path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from config import (
    DATASET_PATH,
    LABEL_ORDER,
    LR_CONFIG,
    MODEL_JOBLIB,
    MODEL_METADATA_JSON,
    MODELS_DIR,
    TARGET_COLUMN,
    TEXT_COLUMN,
    TFIDF_CONFIG,
)
from data_loader import get_feature_arrays, get_class_distribution, load_and_validate
from model import build_pipeline

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
)
logger = logging.getLogger(__name__)


def train_and_save() -> None:
    """
    Train TF-IDF + Logistic Regression on the full dataset and save artefacts.
    """
    logger.info("=== Component 4 GovernanceIntelligence — Full-Dataset Training ===")

    # Step 1 — Load and validate
    df = load_and_validate()
    logger.info("Loaded %d records for final training.", len(df))

    X_list, y_list, _ = get_feature_arrays(df)

    # Step 2 — Build a fresh pipeline
    pipeline = build_pipeline()
    logger.info("Pipeline built: TF-IDF + Logistic Regression.")

    # Step 3 — Train on all records
    pipeline.fit(X_list, y_list)
    logger.info("Training complete. Classes: %s", pipeline.named_steps["clf"].classes_.tolist())

    # Step 4 — Save pipeline
    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, MODEL_JOBLIB)
    logger.info("Model saved to '%s'", MODEL_JOBLIB)

    # Step 5 — Save metadata
    import sklearn
    class_dist = get_class_distribution(df)
    metadata = {
        "component"              : "Component 4 — GovernanceIntelligence",
        "trained_at"             : datetime.now(timezone.utc).isoformat(),
        "dataset_path"           : str(DATASET_PATH),
        "training_row_count"     : len(df),
        "label_order"            : LABEL_ORDER,
        "class_distribution"     : class_dist,
        "text_field"             : TEXT_COLUMN,
        "target_field"           : TARGET_COLUMN,
        "model_architecture"     : "TF-IDF (unigrams+bigrams) + Logistic Regression (L2)",
        "vectorizer_settings"    : {
            **TFIDF_CONFIG,
            "ngram_range": list(TFIDF_CONFIG["ngram_range"]),
        },
        "logistic_regression_settings": LR_CONFIG,
        "sklearn_version"        : sklearn.__version__,
        "python_version"         : sys.version,
        "pretrained_model_used"  : False,
        "pretrained_classifier_used": False,
        "provenance_statement"   : (
            "This classifier was trained from the project dataset only. "
            "No pretrained language model or pretrained classifier was used."
        ),
        "model_artifact"         : str(MODEL_JOBLIB),
        "output_is_advisory"     : True,
        "advisory_note"          : (
            "This model predicts the reported governance-issue category of an "
            "English state-land lease complaint. "
            "Its output is advisory classification intelligence for subsequent "
            "human/governance processing. "
            "It does NOT determine guilt, legal liability, final approval, or "
            "administrative action."
        ),
    }

    with open(MODEL_METADATA_JSON, "w", encoding="utf-8") as f:
        json.dump(metadata, f, indent=2)
    logger.info("Model metadata saved to '%s'", MODEL_METADATA_JSON)

    # Summary
    print("\n" + "=" * 60)
    print("TRAINING COMPLETE")
    print("=" * 60)
    print(f"  Training rows  : {len(df)}")
    print(f"  Labels         : {LABEL_ORDER}")
    print(f"  Class counts   : {class_dist}")
    print(f"  Model artifact : {MODEL_JOBLIB}")
    print(f"  Metadata       : {MODEL_METADATA_JSON}")
    print("=" * 60)


if __name__ == "__main__":
    train_and_save()

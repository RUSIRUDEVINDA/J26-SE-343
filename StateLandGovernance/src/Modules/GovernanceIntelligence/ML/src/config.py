"""
config.py
---------
Centralised configuration for the Component 4 GovernanceIntelligence text-classification ML module.

All paths are resolved relative to this file's location so that scripts work correctly
regardless of the working directory from which they are launched.

Research context
----------------
This classifier belongs to:
    Component 4 — AI-Driven Governance, Compliance and Trust Infrastructure
    Subcomponent: Early Governance and Post-Workflow Anomaly Detection

The classifier predicts the reported governance-issue category of an English
state-land lease complaint.  Its output is advisory classification intelligence
for subsequent human/governance processing.

It does NOT determine guilt, legal liability, final approval, or administrative action.
"""

from pathlib import Path

# ---------------------------------------------------------------------------
# Root paths
# ---------------------------------------------------------------------------

# Directory containing this config file
_SRC_DIR = Path(__file__).resolve().parent

# ML module root  (one level above src/)
ML_ROOT = _SRC_DIR.parent

# Sub-directories
DATA_DIR   = ML_ROOT / "data"
MODELS_DIR = ML_ROOT / "models"
RESULTS_DIR = ML_ROOT / "results"

# ---------------------------------------------------------------------------
# Dataset
# ---------------------------------------------------------------------------

DATASET_PATH = DATA_DIR / "state_land_governance_confirmed_200.csv"

# ---------------------------------------------------------------------------
# Column names
# ---------------------------------------------------------------------------

TEXT_COLUMN   = "Canonical_English_Text"
TARGET_COLUMN = "ML_Label_4Class"
GROUP_COLUMN  = "Source_Group_ID"
ID_COLUMN     = "Research_ID"

# ---------------------------------------------------------------------------
# Fixed four-class taxonomy
# The label order is fixed.  Do not reorder.
# Confusion matrix rows/columns follow this explicit order.
# ---------------------------------------------------------------------------

LABEL_ORDER = [
    "Administrative / Procedural / Integrity",
    "Lease Revenue / Payment / Enforcement",
    "Unauthorized Allocation / Transfer / Use",
    "Protected / Environmental Lease Misuse",
]

# ---------------------------------------------------------------------------
# Cross-validation
# ---------------------------------------------------------------------------

CV_N_SPLITS  = 5
RANDOM_STATE = 42

# ---------------------------------------------------------------------------
# TF-IDF vectoriser settings (frozen for baseline)
# ---------------------------------------------------------------------------

TFIDF_CONFIG = {
    "lowercase"   : True,
    "stop_words"  : "english",
    "ngram_range" : (1, 2),
    "sublinear_tf": True,
}

# ---------------------------------------------------------------------------
# Logistic Regression settings (frozen for baseline)
# ---------------------------------------------------------------------------

LR_CONFIG = {
    "max_iter"     : 2000,
    "class_weight" : "balanced",
    "random_state" : RANDOM_STATE,
    # Defaults kept explicit for documentation purposes:
    "C"            : 1.0,
    "penalty"      : "l2",
    "solver"       : "lbfgs",
    "multi_class"  : "auto",
}

# ---------------------------------------------------------------------------
# Output artefact filenames
# ---------------------------------------------------------------------------

BASELINE_METRICS_JSON      = RESULTS_DIR / "baseline_metrics.json"
FOLD_METRICS_CSV           = RESULTS_DIR / "fold_metrics.csv"
PER_CLASS_METRICS_CSV      = RESULTS_DIR / "per_class_metrics.csv"
CONFUSION_MATRIX_CSV       = RESULTS_DIR / "confusion_matrix.csv"
CONFUSION_MATRIX_PNG       = RESULTS_DIR / "confusion_matrix.png"
OOF_PREDICTIONS_CSV        = RESULTS_DIR / "out_of_fold_predictions.csv"
MISCLASSIFIED_CSV          = RESULTS_DIR / "misclassified_cases.csv"

MODEL_JOBLIB               = MODELS_DIR / "governance_classifier.joblib"
MODEL_METADATA_JSON        = MODELS_DIR / "model_metadata.json"

"""
predict.py
----------
Interactive command-line prediction interface for the Component 4
GovernanceIntelligence text-classification ML module.

Usage
-----
    python src/predict.py          (from ML/ directory)

The script loads the trained model from:
    ML/models/governance_classifier.joblib

and then prompts:
    Enter complaint:

until the user types:
    exit

For every complaint it prints:
    Predicted Governance Category: <category>
    Model probabilities:
        <class>: 0.xxxx  (ordered highest → lowest)

Advisory disclaimer
-------------------
This classifier predicts the reported governance-issue category of an
English state-land lease complaint.  Its output is advisory classification
intelligence for subsequent human/governance processing.

It does NOT determine guilt, legal liability, final approval, or
administrative action.

Probability note
----------------
Probabilities are raw logistic-regression output probabilities.
Calibration has not been performed; do not treat them as calibrated
confidence intervals.
"""

from __future__ import annotations

import sys
from pathlib import Path

import joblib

# Add src/ to path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from config import LABEL_ORDER, MODEL_JOBLIB


_DISCLAIMER = (
    "\n[Advisory] This output is governance classification intelligence only.\n"
    "It does NOT determine guilt, legal liability, or any final administrative action.\n"
)

_PROB_NOTE = (
    "Note: These are model probabilities (not calibrated confidence scores).\n"
)


def load_model():
    """Load the trained pipeline from disk."""
    if not MODEL_JOBLIB.exists():
        print(
            f"ERROR: Model file not found at '{MODEL_JOBLIB}'.\n"
            "Please run 'python src/train.py' first to train and save the model.",
            file=sys.stderr,
        )
        sys.exit(1)
    return joblib.load(MODEL_JOBLIB)


def predict_single(pipeline, complaint: str) -> tuple[str, dict[str, float]]:
    """
    Predict the governance category for a single complaint.

    Returns
    -------
    (predicted_label, proba_dict)
        proba_dict maps each class label to its model probability.
    """
    pred  = pipeline.predict([complaint])[0]
    proba = pipeline.predict_proba([complaint])[0]
    clf_classes = pipeline.named_steps["clf"].classes_.tolist()
    proba_dict  = dict(zip(clf_classes, [float(p) for p in proba]))
    return pred, proba_dict


def run_interactive(pipeline) -> None:
    """Run the interactive prediction loop."""
    print("\n" + "=" * 60)
    print("Component 4 — GovernanceIntelligence Classifier")
    print("Text-only TF-IDF + Logistic Regression (4-class)")
    print("=" * 60)
    print(_DISCLAIMER)
    print("Type a state-land governance complaint and press Enter.")
    print("Type 'exit' to quit.\n")

    while True:
        try:
            complaint = input("Enter complaint:\n> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nExiting.")
            break

        if complaint.lower() == "exit":
            print("Exiting.")
            break

        if not complaint:
            print("(Empty input — please enter a complaint text or 'exit'.)\n")
            continue

        predicted, proba_dict = predict_single(pipeline, complaint)

        print(f"\nPredicted Governance Category:\n  {predicted}\n")
        print("Model probabilities:")

        # Order by probability descending
        sorted_proba = sorted(proba_dict.items(), key=lambda x: x[1], reverse=True)
        for label, prob in sorted_proba:
            print(f"  {label}: {prob:.4f}")

        print()
        print(_PROB_NOTE)
        print("-" * 60)


if __name__ == "__main__":
    pipeline = load_model()
    run_interactive(pipeline)

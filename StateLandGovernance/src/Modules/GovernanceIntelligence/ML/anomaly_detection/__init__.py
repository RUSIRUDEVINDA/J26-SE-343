"""Workflow-anomaly dataset boundary for GovernanceIntelligence.

This package validates and reconciles completed synthetic workflow traces.  It
does not fit, load, or score an anomaly-detection model.
"""

from .features import FEATURE_NAMES, EventRecord, canonicalize_activity, extract_features
from .validation import DatasetValidationError, validate_dataset

__all__ = [
    "DatasetValidationError",
    "EventRecord",
    "FEATURE_NAMES",
    "canonicalize_activity",
    "extract_features",
    "validate_dataset",
]

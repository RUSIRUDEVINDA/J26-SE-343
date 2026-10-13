"""Evaluation dataset manifest schema, validation, and split filtering."""

import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Set

ALLOWED_SPLITS: Set[str] = {"development", "validation", "held_out"}
ALLOWED_LANGUAGE_GROUPS: Set[str] = {"sin", "eng", "mixed"}


@dataclass(frozen=True)
class GroundTruthField:
    """Manually verified ground-truth field label."""

    field_code: str
    expected_value: str
    page_number: int
    evidence_quote: str


@dataclass(frozen=True)
class DocumentManifestEntry:
    """Single document entry in the evaluation manifest."""

    document_id: str
    application_group_id: str
    split: str  # development, validation, held_out
    language_group: str  # sin, eng, mixed
    document_type_ground_truth: str
    file_path: str
    ground_truth_path: Optional[str] = None
    transcription_ground_truth_path: Optional[str] = None

    def validate(self):
        if not self.document_id:
            raise ValueError("document_id must not be empty.")
        if not self.application_group_id:
            raise ValueError("application_group_id must not be empty.")
        if self.split not in ALLOWED_SPLITS:
            raise ValueError(f"split '{self.split}' not in allowed splits: {ALLOWED_SPLITS}")
        if self.language_group not in ALLOWED_LANGUAGE_GROUPS:
            raise ValueError(f"language_group '{self.language_group}' not in allowed groups: {ALLOWED_LANGUAGE_GROUPS}")


@dataclass
class DatasetManifest:
    """Collection of document manifest entries."""

    version: str
    entries: List[DocumentManifestEntry]

    def get_split(self, split: str) -> List[DocumentManifestEntry]:
        if split not in ALLOWED_SPLITS:
            raise ValueError(f"Unknown split '{split}'. Allowed: {ALLOWED_SPLITS}")
        return [e for e in self.entries if e.split == split]

    def get_by_language(self, lang: str) -> List[DocumentManifestEntry]:
        if lang not in ALLOWED_LANGUAGE_GROUPS:
            raise ValueError(f"Unknown language group '{lang}'. Allowed: {ALLOWED_LANGUAGE_GROUPS}")
        return [e for e in self.entries if e.language_group == lang]

    @classmethod
    def load_from_file(cls, path: str | Path) -> "DatasetManifest":
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)

        entries = []
        for item in data.get("documents", []):
            entry = DocumentManifestEntry(
                document_id=item["document_id"],
                application_group_id=item["application_group_id"],
                split=item["split"],
                language_group=item["language_group"],
                document_type_ground_truth=item["document_type_ground_truth"],
                file_path=item["file_path"],
                ground_truth_path=item.get("ground_truth_path"),
                transcription_ground_truth_path=item.get("transcription_ground_truth_path"),
            )
            entry.validate()
            entries.append(entry)

        return cls(version=data.get("version", "1.0"), entries=entries)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "version": self.version,
            "document_count": len(self.entries),
            "documents": [asdict(e) for e in self.entries],
        }


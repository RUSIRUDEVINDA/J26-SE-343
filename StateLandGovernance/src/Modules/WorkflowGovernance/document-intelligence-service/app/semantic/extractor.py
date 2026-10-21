"""Semantic extractor abstractions, Ollama integration, and deterministic stub implementations."""

import abc
import json
import re
import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from app.core.config import settings
from app.core.logging import logger
from app.semantic.catalog import CANONICAL_FIELDS, CRITICAL_FIELD_CODES, DOCUMENT_CLASSIFICATION_TAXONOMY
from app.semantic.chunker import chunk_document_pages
from app.semantic.field_validators import validate_candidate_field
from app.semantic.grounding import ground_evidence
from app.semantic.ollama_client import (
    OllamaClient,
    OllamaConnectionError,
    OllamaError,
    OllamaModelNotFoundError,
    OllamaResponseError,
    OllamaTimeoutError,
)
from app.semantic.prompts import (
    OLLAMA_EXTRACTION_SCHEMA,
    PROMPT_VERSION,
    SYSTEM_INSTRUCTION,
    build_extraction_prompt,
)
from app.semantic.validator import (
    repair_and_parse_json,
    resolve_and_inventory_candidates,
    validate_extracted_payload,
)
from app.semantic.page_understanding import understand_page
from app.semantic.ledger import DocumentEvidenceLedger, LedgerCandidate


@dataclass
class SemanticFactDto:
    """Canonical candidate fact output with grounded evidence and semantic governance metadata."""

    field_code: str
    value_kind: str
    canonical_value: str
    confidence: float
    page_number: int
    quote: str
    start_offset: Optional[int] = None
    end_offset: Optional[int] = None
    is_evidence_verified: bool = False
    validation_warning: Optional[str] = None
    raw_value: Optional[str] = None
    normalized_value: Optional[str] = None
    is_semantic_valid: bool = True
    validation_status: str = "Valid"
    candidate_status: str = "Accepted"
    validation_reasons: List[str] = field(default_factory=list)
    rank: int = 1
    is_preferred: bool = False
    source_type: str = "Explicit"
    normalized_english_value: Optional[str] = None


@dataclass
class SemanticClassificationDto:
    """Document classification output."""

    code: str
    confidence: Optional[float] = None


@dataclass
class SemanticExtractionResult:
    """Complete structured output from semantic analysis."""

    classification: SemanticClassificationDto
    facts: List[SemanticFactDto]
    raw_model_response: str
    prompt_version: str
    model_identifier: str
    duration_ms: float
    warnings: List[str] = field(default_factory=list)
    all_candidates: List[SemanticFactDto] = field(default_factory=list)
    field_inventory: List[Dict[str, Any]] = field(default_factory=list)
    candidate_count: int = 0
    accepted_count: int = 0
    rejected_count: int = 0
    conflict_count: int = 0
    ungrounded_count: int = 0
    is_success: bool = True
    error_message: Optional[str] = None


class BaseSemanticExtractor(abc.ABC):
    """Abstract interface for governed semantic extraction engines."""

    @abc.abstractmethod
    def extract(
        self,
        pages: List[Dict[str, Any]],
        category_hint: Optional[str] = None,
    ) -> SemanticExtractionResult:
        """Execute structured extraction over OCR document pages."""
        pass


class StubSemanticExtractor(BaseSemanticExtractor):
    """
    Deterministic rule-based / regex semantic extractor for CI, unit testing,
    and environments where external models are not loaded.
    Guarantees strict ground-truth extraction and evidence grounding without external dependencies.
    """

    provider_name: str = "stub"

    def __init__(self, model_name: str = "stub-qwen-extractor"):
        self.model_name = model_name
        self.provider_name = "stub"

    def extract(
        self,
        pages: List[Dict[str, Any]],
        category_hint: Optional[str] = None,
    ) -> SemanticExtractionResult:
        start_time = time.perf_counter()
        warnings: List[str] = []

        # Map page texts by page number
        page_texts = {p["page_number"]: p.get("text", "") for p in pages}

        # Deterministic classification heuristic
        full_text = " ".join(page_texts.values()).lower()
        classification_code = "LeaseProposal"
        if "cadastral" in full_text or "survey plan" in full_text:
            classification_code = "CadastralSurveyPlan"
        elif "deed" in full_text or "ඔප්පුව" in full_text:
            classification_code = "TitleDeed"
        elif category_hint and category_hint in DOCUMENT_CLASSIFICATION_TAXONOMY:
            classification_code = category_hint

        # Stage 1: Build ledger from page understanding
        observations = [understand_page(p["page_number"], p.get("text", "")) for p in pages]
        ledger = DocumentEvidenceLedger(observations, page_texts)
        ledger.build_ledger()

        # Stage 2: Reconcile and map to canonical facts
        preferred_facts, all_candidates, field_inventory, counts, add_warnings = ledger.reconcile_and_map_to_canonical_facts()
        warnings.extend(add_warnings)

        duration = (time.perf_counter() - start_time) * 1000.0

        raw_json_debug = {
            "documentClassification": {
                "code": classification_code,
                "confidence": 0.88,
            },
            "facts": [
                {
                    "fieldCode": f.field_code,
                    "value": f.canonical_value,
                    "confidence": f.confidence,
                    "pageNumber": f.page_number,
                    "quote": f.quote,
                }
                for f in preferred_facts
            ],
            "warnings": warnings,
        }

        return SemanticExtractionResult(
            classification=SemanticClassificationDto(code=classification_code, confidence=0.88),
            facts=preferred_facts,
            all_candidates=all_candidates,
            field_inventory=field_inventory,
            candidate_count=counts["candidate_count"],
            accepted_count=counts["accepted_count"],
            rejected_count=counts["rejected_count"],
            conflict_count=counts["conflict_count"],
            ungrounded_count=counts["ungrounded_count"],
            raw_model_response=json.dumps(raw_json_debug, indent=2, ensure_ascii=False),
            prompt_version=PROMPT_VERSION,
            model_identifier=self.model_name,
            duration_ms=round(duration, 2),
            warnings=warnings,
        )


class OllamaQwenSemanticExtractor(BaseSemanticExtractor):
    """
    Real Qwen semantic extractor operating via local Ollama HTTP API.

    Governance Invariants:
    1. Machine output is candidate-only. Extracted facts are non-authoritative.
    2. Model never makes legal, routing, approval, rejection, or completion decisions.
    3. Thinking is disabled (think=False): no hidden reasoning traces generated or stored.
    4. Deterministic inference: temperature=0.0, schema-constrained structured output.
    5. Anti-prompt-injection: untrusted text isolated in <UNTRUSTED_DOCUMENT_PAGES>.
    6. Grounding: evidence quotes verified verbatim against OCR page transcripts.
    7. Precision-first validation: deterministic semantic validation and page priors.
    """

    def __init__(
        self,
        client: Optional[OllamaClient] = None,
        model_name: Optional[str] = None,
        think: Optional[bool] = None,
        temperature: Optional[float] = None,
        keep_alive: Optional[str] = None,
        timeout_seconds: Optional[float] = None,
    ):
        self.client = client or OllamaClient(timeout_seconds=timeout_seconds)
        self.model_name = model_name or settings.ollama_model
        self.provider_name = "qwen"
        self.think = False if think is None else think
        self.temperature = 0.0 if temperature is None else temperature
        self.keep_alive = keep_alive or settings.ollama_keep_alive
        self._model_verified = False

    def _verify_model_installed(self):
        """Verify configured model is installed locally in Ollama; raise clear error if missing."""
        if self._model_verified:
            return

        model_info = self.client.get_model_info(self.model_name)
        if not model_info:
            raise OllamaModelNotFoundError(
                f"Configured Ollama model '{self.model_name}' is not installed. "
                f"Run: ollama pull {self.model_name}"
            )
        self._model_verified = True

    def extract(
        self,
        pages: List[Dict[str, Any]],
        category_hint: Optional[str] = None,
    ) -> SemanticExtractionResult:
        start_time = time.perf_counter()
        warnings: List[str] = []

        # 1. Verify model availability
        self._verify_model_installed()

        # 2. Map page texts by page number for grounding
        page_texts = {p["page_number"]: p.get("text", "") for p in pages}

        # 3. Chunk document pages respecting budget
        chunks = chunk_document_pages(
            pages,
            max_characters_per_chunk=settings.semantic_max_input_length,
        )

        raw_responses: List[str] = []
        overall_classification = SemanticClassificationDto(code="Unknown", confidence=0.0)
        all_candidates: List[SemanticFactDto] = []

        # Stage 1: Build ledger from page understanding observations
        observations = [understand_page(p["page_number"], p.get("text", "")) for p in pages]
        ledger = DocumentEvidenceLedger(observations, page_texts)
        ledger.build_ledger()

        # Stage 2: Process each chunk through Ollama to enrich candidate observations
        for chunk in chunks:
            prompt = build_extraction_prompt(chunk.pages, category_hint=category_hint)
            messages = [
                {"role": "system", "content": SYSTEM_INSTRUCTION},
                {"role": "user", "content": prompt},
            ]

            try:
                response = self.client.chat(
                    model=self.model_name,
                    messages=messages,
                    schema=OLLAMA_EXTRACTION_SCHEMA,
                    think=self.think,
                    temperature=self.temperature,
                    keep_alive=self.keep_alive,
                )
            except OllamaError as ex:
                logger.error(f"Ollama chat error for chunk {chunk.chunk_index}: {ex}")
                warnings.append(f"Chunk {chunk.chunk_index} Ollama failure: {str(ex)}")
                continue

            msg = response.get("message", {})
            raw_text = msg.get("content", "").strip()
            raw_responses.append(raw_text)

            # Parse and validate structured output
            parsed_json, json_err = repair_and_parse_json(raw_text)
            if json_err:
                warnings.append(f"Chunk {chunk.chunk_index}: {json_err}")
                continue

            sanitized_payload, val_warnings = validate_extracted_payload(parsed_json)
            warnings.extend(val_warnings)

            # Update overall classification if chunk confidence is higher
            chunk_class = sanitized_payload.get("documentClassification", {})
            class_conf = chunk_class.get("confidence", 0.0)
            current_conf = overall_classification.confidence if overall_classification.confidence is not None else -1.0
            if class_conf > current_conf:
                overall_classification = SemanticClassificationDto(
                    code=chunk_class.get("code", "Unknown"),
                    confidence=class_conf,
                )

            # Ingest grounded candidate facts into document evidence ledger
            for f in sanitized_payload.get("facts", []):
                p_num = f["pageNumber"]
                quote = f["quote"]
                grounding = ground_evidence(page_texts, p_num, quote)

                if not grounding.is_verified and grounding.warning:
                    warnings.append(grounding.warning)

                if grounding.is_verified:
                    ledger.add_candidate(
                        LedgerCandidate(
                            field_code=f["fieldCode"],
                            canonical_value=f["canonicalValue"],
                            raw_value=f.get("rawValue", f["canonicalValue"]),
                            normalized_english_value=f.get("normalizedValue"),
                            source_type="SemanticSummary",
                            page_number=grounding.page_number,
                            quote=grounding.quote,
                            start_offset=grounding.start_offset,
                            end_offset=grounding.end_offset,
                            confidence=min(f["confidence"], 0.85),
                            reconciliation_notes=f"Extracted by Qwen semantic model from page {grounding.page_number}",
                        )
                    )

        if not raw_responses:
            # Semantic inference did not complete for any chunk
            overall_classification = SemanticClassificationDto(code="Unavailable", confidence=None)
            warnings.append(
                "Semantic inference did not complete. No valid classification or candidate facts could be extracted."
            )
            field_inventory = [
                {
                    "field_code": code,
                    "display_name": f_def.display_name,
                    "status": "Missing",
                    "preferred_value": None,
                    "raw_value": None,
                    "page_number": None,
                    "confidence": None,
                    "is_grounded": False,
                    "candidate_count": 0,
                    "source_type": None,
                    "normalized_english_value": None,
                }
                for code, f_def in CANONICAL_FIELDS.items()
            ]
            duration = (time.perf_counter() - start_time) * 1000.0
            return SemanticExtractionResult(
                classification=overall_classification,
                facts=[],
                all_candidates=[],
                field_inventory=field_inventory,
                candidate_count=0,
                accepted_count=0,
                rejected_count=0,
                conflict_count=0,
                ungrounded_count=0,
                raw_model_response="",
                prompt_version=PROMPT_VERSION,
                model_identifier=self.model_name,
                duration_ms=round(duration, 2),
                warnings=warnings,
            )

        # Document Classification Hardening:
        # Determine classification using generic structural features across early pages and whole-document context
        doc_header = (page_texts.get(1, "") + " " + page_texts.get(2, "")).lower()
        proposal_structural_terms = [
            "proposal", "lease application", "project proposal", "development proposal",
            "investment proposal", "feasibility study", "project report", "project plan",
            "application for lease", "business proposal", "ව්‍යාපෘති යෝජනාව", "ව්‍යාපෘති වාර්තාව",
            "ව්‍යාපෘති සැලැස්ම", "බදු අයදුම්පත", "ඉඩම් බදු", "යෝජිත ව්‍යාපෘතිය", "බදු ඉල්ලුම්පත"
        ]
        has_proposal_structure = any(term in doc_header for term in proposal_structural_terms)

        # Check if AffidavitOfNoEncumbrance was hallucinated without any sworn affidavit language
        full_text_lower = " ".join(page_texts.values()).lower()
        has_affidavit_language = any(term in full_text_lower for term in ["affidavit", "දිවුරුම්", "affirmation", "sworn before"])

        if overall_classification.code == "AffidavitOfNoEncumbrance" and not has_affidavit_language:
            if has_proposal_structure or category_hint == "LeaseProposal":
                overall_classification = SemanticClassificationDto(code="LeaseProposal", confidence=0.88)
                warnings.append("Document classification corrected from unsupported 'AffidavitOfNoEncumbrance' to 'LeaseProposal' based on proposal structure.")
            else:
                overall_classification = SemanticClassificationDto(code="Other", confidence=0.50)
                warnings.append("Document lacks sworn affidavit language; classification adjusted.")
        elif has_proposal_structure and overall_classification.code in ("SurveyReport", "CadastralSurveyPlan", "Other", "Unknown", "Unavailable"):
            overall_classification = SemanticClassificationDto(code="LeaseProposal", confidence=0.90)
            warnings.append("Document classification adjusted to 'LeaseProposal' based on generic structural proposal indicators.")

        # Stage 3: Reconcile candidates across pages and map to 17-field catalog inventory
        preferred_facts, all_candidates, field_inventory, counts, add_warnings = ledger.reconcile_and_map_to_canonical_facts()
        warnings.extend(add_warnings)

        duration = (time.perf_counter() - start_time) * 1000.0

        return SemanticExtractionResult(
            classification=overall_classification,
            facts=preferred_facts,
            all_candidates=all_candidates,
            field_inventory=field_inventory,
            candidate_count=counts["candidate_count"],
            accepted_count=counts["accepted_count"],
            rejected_count=counts["rejected_count"],
            conflict_count=counts["conflict_count"],
            ungrounded_count=counts["ungrounded_count"],
            raw_model_response="\n---\n".join(raw_responses),
            prompt_version=PROMPT_VERSION,
            model_identifier=self.model_name,
            duration_ms=round(duration, 2),
            warnings=warnings,
        )


# Backward-compatibility alias
QwenSemanticExtractor = OllamaQwenSemanticExtractor


def get_semantic_extractor(provider: Optional[str] = None) -> BaseSemanticExtractor:
    """Factory resolving configured semantic extractor."""
    p = (provider or settings.semantic_provider).strip().lower()
    if p in ("ollama", "qwen"):
        return OllamaQwenSemanticExtractor()
    elif p == "gemini":
        from app.semantic.gemini_extractor import GeminiSemanticExtractor
        return GeminiSemanticExtractor()
    elif p == "stub":
        return StubSemanticExtractor()
    else:
        logger.warning(
            f"Unknown semantic provider '{p}'. Falling back to StubSemanticExtractor."
        )
        return StubSemanticExtractor()

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
from app.semantic.validator import repair_and_parse_json, validate_extracted_payload


@dataclass
class SemanticFactDto:
    """Canonical candidate fact output with grounded evidence."""

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


@dataclass
class SemanticClassificationDto:
    """Document classification output."""

    code: str
    confidence: float


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

    def __init__(self, model_name: str = "stub-qwen-extractor"):
        self.model_name = model_name

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

        extracted_raw_facts: List[Dict[str, Any]] = []

        # Heuristic matchers for common proposal fields
        patterns = [
            (
                "applicant_name",
                r"(?:applicant(?:\s+name)?|name\s+of\s+applicant|අයදුම්කරුගේ\s+නම)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "applicant_type",
                r"(?:applicant\s+type|entity\s+type)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "project_title",
                r"(?:project(?:\s+title)?|title\s+of\s+project|ව්‍යාපෘතියේ\s+නම)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "land_location",
                r"(?:land\s+location|location\s+of\s+land|ඉඩමේ\s+පිහිටීම)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "district",
                r"(?:district|දිස්ත්‍රික්කය)\s*[:\-]?\s*(Hambantota|Kandy|Colombo|Gampaha|Galle|Matara|Kurunegala|Anuradhapura|හම්බන්තොට|මහනුවර|කොළඹ)",
                "Text",
            ),
            (
                "divisional_secretariat_division",
                r"(?:divisional\s+secretariat(?:\s+division)?|ds\s+division|ප්‍රාදේශීය\s+ලේකම්\s+කොට්ඨාසය)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "land_extent",
                r"(?:land\s+extent|extent\s+of\s+land|extent|ප්‍රමාණය)\s*[:\-]\s*(\d+(?:\.\d+)?(?:\s*(?:acres?|hectares?|perches?|අක්කර|හෙක්ටයාර))?)",
                "Text",
            ),
            (
                "requested_lease_period",
                r"(?:lease\s+period|requested\s+period|period|කාලය)\s*[:\-]\s*(\d+\s*(?:years?|මාස|අවුරුදු))",
                "Text",
            ),
            (
                "proposed_land_use",
                r"(?:proposed\s+land\s+use|land\s+use|භාවිතය)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "investment_amount",
                r"(?:investment(?:\s+amount)?|estimated\s+investment|ආයෝජනය)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
            (
                "legal_statutory_basis",
                r"(?:statutory\s+basis|legal\s+basis|ordinance|ව්‍යවස්ථාපිත\s+පදනම)\s*[:\-]\s*([^\n\r,\.]+)",
                "Text",
            ),
        ]

        seen_codes = set()
        for page in pages:
            p_num = page["page_number"]
            p_text = page.get("text", "")
            for code, regex, kind in patterns:
                if code in seen_codes:
                    continue
                match = re.search(regex, p_text, flags=re.IGNORECASE)
                if match:
                    raw_val = match.group(1).strip()
                    quote = match.group(0).strip()
                    seen_codes.add(code)
                    extracted_raw_facts.append({
                        "fieldCode": code,
                        "valueKind": kind,
                        "canonicalValue": raw_val,
                        "confidence": 0.90,
                        "pageNumber": p_num,
                        "quote": quote,
                    })

        # Ground each extracted fact against OCR pages
        grounded_facts: List[SemanticFactDto] = []
        for f in extracted_raw_facts:
            g = ground_evidence(page_texts, f["pageNumber"], f["quote"])
            if not g.is_verified and g.warning:
                warnings.append(g.warning)

            grounded_facts.append(
                SemanticFactDto(
                    field_code=f["fieldCode"],
                    value_kind=f["valueKind"],
                    canonical_value=f["canonicalValue"],
                    confidence=f["confidence"],
                    page_number=g.page_number,
                    quote=g.quote,
                    start_offset=g.start_offset,
                    end_offset=g.end_offset,
                    is_evidence_verified=g.is_verified,
                    validation_warning=g.warning,
                )
            )

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
                for f in grounded_facts
            ],
            "warnings": warnings,
        }

        return SemanticExtractionResult(
            classification=SemanticClassificationDto(code=classification_code, confidence=0.88),
            facts=grounded_facts,
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
    """

    def __init__(
        self,
        client: Optional[OllamaClient] = None,
        model_name: Optional[str] = None,
        think: Optional[bool] = None,
        temperature: Optional[float] = None,
        keep_alive: Optional[str] = None,
    ):
        self.client = client or OllamaClient()
        self.model_name = model_name or settings.ollama_model
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
        extracted_facts_by_field: Dict[str, List[SemanticFactDto]] = {}

        # 4. Process each chunk through Ollama
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
            if class_conf > overall_classification.confidence:
                overall_classification = SemanticClassificationDto(
                    code=chunk_class.get("code", "Unknown"),
                    confidence=class_conf,
                )

            # Ground candidate facts against OCR page text
            for f in sanitized_payload.get("facts", []):
                p_num = f["pageNumber"]
                quote = f["quote"]
                grounding = ground_evidence(page_texts, p_num, quote)

                if not grounding.is_verified and grounding.warning:
                    warnings.append(grounding.warning)

                fact_dto = SemanticFactDto(
                    field_code=f["fieldCode"],
                    value_kind=f["valueKind"],
                    canonical_value=f["canonicalValue"],
                    confidence=f["confidence"],
                    page_number=grounding.page_number,
                    quote=grounding.quote,
                    start_offset=grounding.start_offset,
                    end_offset=grounding.end_offset,
                    is_evidence_verified=grounding.is_verified,
                    validation_warning=grounding.warning,
                )

                field_code = f["fieldCode"]
                if field_code not in extracted_facts_by_field:
                    extracted_facts_by_field[field_code] = []
                extracted_facts_by_field[field_code].append(fact_dto)

        # 5. Deterministic Fact Merging across Chunks
        merged_facts: List[SemanticFactDto] = []
        for field_code, candidates in extracted_facts_by_field.items():
            if len(candidates) == 1:
                merged_facts.append(candidates[0])
                continue

            # Group candidates by normalized value
            by_val: Dict[str, List[SemanticFactDto]] = {}
            for c in candidates:
                val_key = c.canonical_value.strip().lower()
                if val_key not in by_val:
                    by_val[val_key] = []
                by_val[val_key].append(c)

            if len(by_val) == 1:
                # Same field, same value -> merge: select candidate with verified evidence / highest confidence
                best = max(
                    candidates,
                    key=lambda c: (1 if c.is_evidence_verified else 0, c.confidence),
                )
                merged_facts.append(best)
            else:
                # Same field, different values -> preserve conflicting candidates and add warning
                warnings.append(
                    f"Conflicting candidates extracted for field '{field_code}': "
                    f"{[c.canonical_value for c in candidates]}. Retaining candidates for human verification."
                )
                for c in candidates:
                    merged_facts.append(c)

        duration = (time.perf_counter() - start_time) * 1000.0

        return SemanticExtractionResult(
            classification=overall_classification,
            facts=merged_facts,
            raw_model_response="\n---\n".join(raw_responses),
            prompt_version=PROMPT_VERSION,
            model_identifier=self.model_name,
            duration_ms=round(duration, 2),
            warnings=warnings,
        )


# Backward-compatibility alias
QwenSemanticExtractor = OllamaQwenSemanticExtractor


def get_semantic_extractor() -> BaseSemanticExtractor:
    """Factory resolving configured semantic extractor."""
    provider = settings.semantic_provider.strip().lower()
    if provider in ("ollama", "qwen"):
        # Use real Ollama extractor if provider is ollama or live tests are requested
        return OllamaQwenSemanticExtractor()
    elif provider == "stub":
        return StubSemanticExtractor()
    else:
        logger.warning(
            f"Unknown semantic provider '{provider}'. Falling back to StubSemanticExtractor."
        )
        return StubSemanticExtractor()

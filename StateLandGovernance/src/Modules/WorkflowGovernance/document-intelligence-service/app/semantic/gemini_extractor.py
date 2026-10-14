"""
Google Gemini Semantic Extractor for Governed Proposal Ingestion.

Implements BaseSemanticExtractor using Google Gemini (default: gemini-3.8-flash)
via direct HTTP REST client.

Research & Governance Invariants:
1. Uses existing Tesseract OCR output with explicit page boundaries (=== PAGE X ===).
2. Gemini is strictly a candidate extractor — never authoritative.
3. Every candidate evidence quote is verified against the original OCR transcript
   using the common deterministic grounding engine (ground_evidence).
4. Deterministic field validators (validate_candidate_field) enforce type, range,
   and administrative dictionary rules.
5. Translations alone are never accepted as evidence; ungrounded facts are flagged.
6. Missing fields remain strictly Missing (no hallucinated defaults).
7. Independent from Qwen — no Qwen intermediate state is consumed.
"""

import json
import logging
import re
import time
from typing import Any, Dict, List, Optional

from app.core.config import settings
from app.models.schemas import GovernedFieldInventoryItem
from app.semantic.catalog import CANONICAL_FIELDS, DOCUMENT_CLASSIFICATION_TAXONOMY
from app.semantic.extractor import (
    BaseSemanticExtractor,
    SemanticClassificationDto,
    SemanticExtractionResult,
    SemanticFactDto,
)
from app.semantic.field_validators import validate_candidate_field
from app.semantic.gemini_client import (
    GeminiAuthenticationError,
    GeminiBadRequestError,
    GeminiClient,
    GeminiError,
    GeminiNotFoundError,
    GeminiRateLimitError,
    GeminiResponseError,
    GeminiServerError,
    GeminiTimeoutError,
)
from app.semantic.grounding import GroundedEvidence, ground_evidence

logger = logging.getLogger(__name__)

GEMINI_PROMPT_VERSION = "gemini-3.8-flash-proposal-v1"

# ============================================================================
# SYSTEM INSTRUCTION FOR GEMINI
# ============================================================================

GEMINI_SYSTEM_INSTRUCTION = """You are an expert Sri Lankan land administration document intelligence specialist.
Your task is to analyze the OCR transcript of an official state land lease proposal and extract structured candidate facts conforming strictly to the governed 17-field catalog.

IMPORTANT GENERAL PRINCIPLES:
1. ORIGINAL OCR IS AUTHORITATIVE EVIDENCE:
   - For every extracted field, you MUST provide an exact, verbatim quote ('originalEvidenceQuotes') from the provided OCR text of that specific page.
   - Do NOT translate evidence quotes into English. The evidence quote MUST match the original Sinhala or English OCR text exactly.
   - If you cannot find a verbatim quote in the OCR text, do NOT extract the field.
2. ENGLISH NORMALIZATION:
   - Provide a concise English normalized interpretation in 'normalizedEnglishValue' to assist administrative officers.
3. STRICT ABSTENTION (MISSING FIELDS):
   - Only extract fields that are genuinely present in the document.
   - Do NOT invent, assume, or hallucinate values.
   - If a field is not stated in the document, OMIT it from 'extractedFields'.

FIELD-SPECIFIC SEMANTIC BOUNDARIES:
- applicant_name: The individual person or entity applying for the lease (found in applicant details or header).
  CRITICAL: Do NOT extract government officers, recommending officials (e.g. Coconut Development Officer, Grama Niladhari, Divisional Secretary), or witnesses as the applicant.
- applicant_type: 'Individual', 'Private Company', 'Public Company', 'Cooperative', or 'Government Institution'.
- project_title: The explicit formal name of the proposed project (often on cover page or under project details).
  Do NOT extract administrative locations, officer titles, or document headings as the project title.
- project_purpose: The stated objective, reason, or rationale for requesting the land (e.g. commercial coconut cultivation, dairy farming).
  Do NOT extract pure location names, district names, or officer designations as the purpose.
- land_location: The specific local address, village, or land plot location (e.g. Grama Niladhari division, land name).
  Do NOT extract officer designations or applicant names as land location.
- district: Canonical Sri Lankan administrative district (e.g. Puttalam, Kurunegala, Anuradhapura, Colombo). If inferred from DSD or locality, mark sourceType as 'Derived'.
- divisional_secretariat_division: The Divisional Secretariat Division (DSD) under which the land falls (e.g. Mahakumbukkadawala, Anamaduwa).
- land_extent: The numerical extent of the requested land (e.g. 17).
- land_extent_unit: Standardized unit: 'Acre', 'Hectare', or 'Rood'.
- requested_lease_period: The duration for which the state land LEASE is requested.
  CRITICAL: Only extract if context explicitly states the lease duration or requested tenure. Do NOT convert a 10-year or 15-year business plan, construction schedule, or revenue projection period into the lease period! If no lease duration is explicitly stated, leave this field MISSING.
- proposed_land_use: Intended physical use of the land (e.g. Agriculture / Coconut Cultivation).
- investment_amount: The stated total/initial project capital investment. Distinguish capital expenditure from operating costs, annual turnover, or loan amounts.
  If an explicit total row is printed/stated in the text, extract that total with sourceType 'Explicit'.
  If an itemized capital investment table (such as Section 09 / ව්‍යාපෘතියේ මුළු පිරිවැය / මුළු ආයෝජනය) provides clearly stated capital expenditure rows (such as land development, buildings, machinery, and other capital expenses) with currency context, but the bottom total row is degraded or unprinted in the OCR text:
  You may compute the derived capital expenditure total as the sum of these grounded itemized rows.
  In this case:
  - Set 'sourceType' to 'Derived'.
  - In 'originalEvidenceQuotes', provide verbatim quotes for EACH component row amount and the table heading/currency from that page.
  - In 'reason', clearly list the component amounts summed and their total.
- investment_currency: Currency of investment ('LKR', 'USD', etc.). Must be explicitly grounded in currency symbols/words (e.g. 'රු.', 'LKR', 'Rs.') from the financial table heading or cells. Do not infer unsupported currency.
- expected_employment: Headcount of expected direct/indirect employment. Must be an actual headcount statement, not labour financial cost. If breakdown is given without a fixed single total, or if stated as variable/seasonal, leave MISSING or report exact breakdown.
- project_description: Concise 1-3 sentence summary of the project as stated in the introduction or overview.
- supporting_document_references: Identifiable attached/supporting documents (e.g. survey plan number, bank statement, registration certificate).
- legal_statutory_basis: Explicit statutory instrument cited (e.g. State Lands Ordinance, Land Development Ordinance). Do NOT infer a law from standard administrative language; if not explicitly cited, leave MISSING.
"""

# ============================================================================
# GEMINI STRUCTURED JSON SCHEMA
# ============================================================================

GEMINI_RESPONSE_SCHEMA: Dict[str, Any] = {
    "type": "OBJECT",
    "properties": {
        "documentClassification": {
            "type": "OBJECT",
            "properties": {
                "code": {
                    "type": "STRING",
                    "enum": list(DOCUMENT_CLASSIFICATION_TAXONOMY),
                },
                "confidence": {"type": "NUMBER"},
                "reason": {"type": "STRING"},
            },
            "required": ["code", "confidence"],
        },
        "extractedFields": {
            "type": "ARRAY",
            "items": {
                "type": "OBJECT",
                "properties": {
                    "fieldCode": {
                        "type": "STRING",
                        "enum": list(CANONICAL_FIELDS.keys()),
                    },
                    "value": {
                        "type": "STRING",
                        "description": "Extracted canonical value verbatim from text.",
                    },
                    "normalizedEnglishValue": {
                        "type": "STRING",
                        "description": "Concise English semantic translation/normalization.",
                    },
                    "sourceType": {
                        "type": "STRING",
                        "enum": ["Explicit", "Derived", "SemanticSummary"],
                    },
                    "pageNumbers": {
                        "type": "ARRAY",
                        "items": {"type": "INTEGER"},
                        "description": "1-based page numbers where evidence was observed.",
                    },
                    "originalEvidenceQuotes": {
                        "type": "ARRAY",
                        "items": {"type": "STRING"},
                        "description": "Exact verbatim quote from the original OCR text on the cited page.",
                    },
                    "confidence": {
                        "type": "NUMBER",
                        "description": "Confidence score between 0.0 and 1.0.",
                    },
                    "reason": {
                        "type": "STRING",
                        "description": "Brief justification for this extraction.",
                    },
                },
                "required": [
                    "fieldCode",
                    "value",
                    "sourceType",
                    "pageNumbers",
                    "originalEvidenceQuotes",
                    "confidence",
                ],
            },
        },
    },
    "required": ["documentClassification", "extractedFields"],
}


# ============================================================================
# GEMINI SEMANTIC EXTRACTOR
# ============================================================================

class GeminiSemanticExtractor(BaseSemanticExtractor):
    """
    Semantic extractor powered by Google Gemini (gemini-3.8-flash).
    
    Processes full OCR transcripts in a single context window, parses structured
    candidate facts, and executes common deterministic grounding and validation.
    """

    def __init__(
        self,
        client: Optional[GeminiClient] = None,
        model_name: Optional[str] = None,
        api_key: Optional[str] = None,
        temperature: float = 0.0,
        raise_on_error: bool = False,
    ):
        self.client = client or GeminiClient(api_key=api_key, model_name=model_name)
        self.model_name = self.client.model_name
        self.provider_name = "gemini"
        self.temperature = temperature
        self.raise_on_error = raise_on_error


    def extract(
        self,
        pages: List[Dict[str, Any]],
        category_hint: Optional[str] = None,
    ) -> SemanticExtractionResult:
        """
        Executes end-to-end semantic extraction on OCR page texts via Gemini.
        Applies common deterministic evidence grounding, field validation,
        and inventory generation.
        """
        start_time = time.perf_counter()
        warnings: List[str] = []

        if not pages:
            duration = (time.perf_counter() - start_time) * 1000.0
            return SemanticExtractionResult(
                classification=SemanticClassificationDto(code="Unknown", confidence=0.0),
                facts=[],
                all_candidates=[],
                field_inventory=[
                    GovernedFieldInventoryItem(
                        field_code=code,
                        display_name=f_def.display_name,
                        status="Missing",
                    )
                    for code, f_def in CANONICAL_FIELDS.items()
                ],
                candidate_count=0,
                accepted_count=0,
                rejected_count=0,
                conflict_count=0,
                ungrounded_count=0,
                raw_model_response="",
                prompt_version=GEMINI_PROMPT_VERSION,
                model_identifier=self.model_name,
                duration_ms=round(duration, 2),
                warnings=["Document contains zero pages."],
            )

        page_texts = {p["page_number"]: p.get("text", "") for p in pages}

        # 1. Format document OCR input preserving page boundaries
        doc_parts = []
        for p in pages:
            p_num = p["page_number"]
            p_text = p.get("text", "").strip()
            doc_parts.append(f"=== PAGE {p_num} ===\n{p_text}")
        full_ocr_input = "\n\n".join(doc_parts)

        # 2. Build user prompt
        user_prompt = (
            f"Please extract the governed proposal fields from the following official state land document OCR transcript.\n"
            f"Category hint: {category_hint or 'Unspecified'}\n\n"
            f"{full_ocr_input}\n\n"
            f"Remember: Output structured JSON conforming to the schema. "
            f"Every field must have an exact verbatim quote from the OCR text on that page. "
            f"If a field is not present in the document, omit it."
        )

        # 3. Call Gemini API
        try:
            gemini_response = self.client.generate_content(
                prompt=user_prompt,
                system_instruction=GEMINI_SYSTEM_INSTRUCTION,
                schema=GEMINI_RESPONSE_SCHEMA,
                temperature=self.temperature,
            )
        except GeminiAuthenticationError as ex:
            logger.error(f"Gemini authentication failed: {ex}")
            warnings.append(str(ex))
            if self.raise_on_error:
                raise
            return self._build_unavailable_result(start_time, warnings, error_msg=str(ex))
        except (GeminiTimeoutError, GeminiRateLimitError, GeminiServerError, GeminiResponseError, GeminiError) as ex:
            logger.error(f"Gemini API execution failure: {ex}")
            warnings.append(f"Gemini API failure: {str(ex)}")
            if self.raise_on_error:
                raise
            return self._build_unavailable_result(start_time, warnings, error_msg=str(ex))

        duration = (time.perf_counter() - start_time) * 1000.0
        return self.extract_from_response(
            gemini_response=gemini_response,
            pages=pages,
            duration_ms=duration,
            warnings=warnings,
        )

    def extract_from_response(
        self,
        gemini_response: Dict[str, Any],
        pages: List[Dict[str, Any]],
        duration_ms: Optional[float] = None,
        warnings: Optional[List[str]] = None,
    ) -> SemanticExtractionResult:
        """
        Parses, grounds, validates, and reconciles candidates from a structured Gemini JSON response.
        Allows offline evaluation and replaying of responses against authentic OCR pages without API calls.
        """
        warnings = list(warnings or [])
        page_texts = {p.get("page_number") or p.get("pageNumber", idx): p.get("text") or p.get("cleanText", "") for idx, p in enumerate(pages, 1)}

        # Handle raw Google API response envelope if passed directly
        if isinstance(gemini_response, dict) and "candidates" in gemini_response:
            try:
                candidate = gemini_response["candidates"][0]
                text_content = candidate.get("content", {}).get("parts", [{}])[0].get("text", "").strip()
                if text_content.startswith("```"):
                    text_content = text_content.split("\n", 1)[-1].rsplit("```", 1)[0].strip()
                gemini_response = json.loads(text_content)
            except Exception as e:
                logger.warning(f"Could not unwrap candidates envelope: {e}")

        # 4. Parse document classification
        doc_class_data = gemini_response.get("documentClassification", {})
        classification_code = doc_class_data.get("code", "Unknown")
        if classification_code not in DOCUMENT_CLASSIFICATION_TAXONOMY:
            classification_code = "Other"
        classification_conf = doc_class_data.get("confidence", 0.5)
        overall_classification = SemanticClassificationDto(
            code=classification_code,
            confidence=classification_conf,
        )

        # 5. Process candidate fields through COMMON deterministic grounding and validation
        raw_fields = gemini_response.get("extractedFields", [])
        candidates_by_field: Dict[str, List[SemanticFactDto]] = {code: [] for code in CANONICAL_FIELDS}
        all_candidates_list: List[SemanticFactDto] = []

        for f_data in raw_fields:
            code = f_data.get("fieldCode")
            if code not in CANONICAL_FIELDS:
                continue

            raw_val = str(f_data.get("value") or f_data.get("rawValue") or "").strip()
            norm_en = f_data.get("normalizedEnglishValue")
            src_type = f_data.get("sourceType", "Explicit")
            conf = float(f_data.get("confidence", 0.8))
            conf = max(0.0, min(1.0, conf))

            # Resolve page number and evidence quote
            page_nums = f_data.get("pageNumbers") or ([f_data["pageNumber"]] if "pageNumber" in f_data else [])
            candidate_pages = [p for p in page_nums if isinstance(p, int) and p in page_texts] or ([page_nums[0]] if page_nums else [1])
            quotes = f_data.get("originalEvidenceQuotes", [])
            candidate_quotes = [q.strip() for q in quotes if q and q.strip()] or [raw_val.strip()]

            # COMMON DETERMINISTIC EVIDENCE GROUNDING: verify across cited pages and quotes
            best_grounding = None
            page_num = candidate_pages[0] if candidate_pages else 1
            quote = candidate_quotes[0] if candidate_quotes else raw_val

            if src_type == "Derived" and len(candidate_quotes) > 1:
                # Derived candidate supported by multiple operand quotes
                grounded_operands = []
                primary_page = candidate_pages[0] if candidate_pages else 1
                for q_c in candidate_quotes:
                    g_op = ground_evidence(page_texts, primary_page, q_c)
                    if not g_op.is_verified:
                        for p_alt in candidate_pages:
                            g_alt = ground_evidence(page_texts, p_alt, q_c)
                            if g_alt.is_verified:
                                g_op = g_alt
                                break
                    if g_op.is_verified:
                        grounded_operands.append((q_c, g_op))

                if grounded_operands and len(grounded_operands) == len(candidate_quotes):
                    # All operand quotes strictly grounded in OCR transcript
                    quote = " | ".join(candidate_quotes)
                    page_num = primary_page
                    best_grounding = GroundedEvidence(
                        page_number=page_num,
                        quote=quote,
                        start_offset=grounded_operands[0][1].start_offset,
                        end_offset=grounded_operands[-1][1].end_offset,
                        is_verified=True,
                        warning=None,
                    )
                elif len(grounded_operands) >= 2:
                    quote = " | ".join([q for q, _ in grounded_operands])
                    page_num = primary_page
                    best_grounding = GroundedEvidence(
                        page_number=page_num,
                        quote=quote,
                        start_offset=grounded_operands[0][1].start_offset,
                        end_offset=grounded_operands[-1][1].end_offset,
                        is_verified=True,
                        warning=f"Partially grounded operands ({len(grounded_operands)}/{len(candidate_quotes)}).",
                    )

            if not best_grounding:
                for p_candidate in candidate_pages:
                    for q_candidate in candidate_quotes:
                        g = ground_evidence(page_texts, p_candidate, q_candidate)
                        if g.is_verified:
                            best_grounding = g
                            page_num = p_candidate
                            quote = q_candidate
                            break
                    if best_grounding and best_grounding.is_verified:
                        break

            if not best_grounding:
                grounding = ground_evidence(page_texts, page_num, quote)
            else:
                grounding = best_grounding

            # Common deterministic field validation
            val_result = validate_candidate_field(
                field_code=code,
                value=raw_val,
                quote=quote,
                page_number=page_num,
            )

            # Resolve status
            if not grounding.is_verified:
                cand_status = "Ungrounded"
                warnings.append(
                    f"Gemini candidate for '{code}' on page {page_num} could not be verified against OCR text."
                )
            elif not val_result.is_valid:
                cand_status = "Rejected"
            else:
                cand_status = "Accepted"

            reasons = list(val_result.reasons)

            # Arithmetic check for derived investment amounts
            if code == "investment_amount" and src_type == "Derived":
                derived_numbers = []
                for q_c in candidate_quotes:
                    m_nums = re.findall(r"\b\d{1,3}(?:,\d{3})*(?:\.\d+)?\b|\b\d{4,}(?:\.\d+)?\b", q_c)
                    for n in m_nums:
                        try:
                            val_f = float(n.replace(",", ""))
                            if val_f > 0:
                                derived_numbers.append(val_f)
                        except ValueError:
                            pass
                if derived_numbers:
                    calc_sum = sum(derived_numbers)
                    raw_num_match = re.search(r"\b\d{1,3}(?:,\d{3})*(?:\.\d+)?\b|\b\d{4,}(?:\.\d+)?\b", raw_val)
                    if raw_num_match:
                        raw_f = float(raw_num_match.group(0).replace(",", ""))
                        if abs(calc_sum - raw_f) < 1.0:
                            reasons.append(f"Derived arithmetic verified: sum({[f'{n:,.2f}' for n in derived_numbers]}) == {raw_f:,.2f}.")
                        else:
                            reasons.append(f"Arithmetic note: sum of detected operands {calc_sum:,.2f} vs candidate {raw_f:,.2f}.")
                reasons.append("Derived arithmetic sum from grounded table operands; requires human verification.")

            if f_data.get("reason"):
                reasons.append(f"Model rationale: {f_data['reason']}")

            # Domain invariant: remove control characters from canonical value
            canon_val = val_result.normalized_value or raw_val
            canon_val = re.sub(r"[\r\n\t\x00-\x1f\x7f-\x9f]+", " ", canon_val).strip()

            fact_dto = SemanticFactDto(
                field_code=code,
                value_kind=CANONICAL_FIELDS[code].value_kind,
                canonical_value=canon_val,
                raw_value=raw_val,
                normalized_value=norm_en or val_result.normalized_value,
                confidence=conf,
                page_number=page_num,
                quote=quote,
                start_offset=grounding.start_offset,
                end_offset=grounding.end_offset,
                is_evidence_verified=grounding.is_verified,
                validation_warning=grounding.warning,
                is_semantic_valid=val_result.is_valid,
                validation_status="Valid" if val_result.is_valid else "Invalid",
                candidate_status=cand_status,
                validation_reasons=reasons,
                rank=1,
                is_preferred=False,
                source_type=src_type,
                normalized_english_value=norm_en,
            )

            candidates_by_field[code].append(fact_dto)
            all_candidates_list.append(fact_dto)

        # 6. Resolve ranking and preferred candidates per field
        preferred_facts: List[SemanticFactDto] = []
        field_inventory: List[GovernedFieldInventoryItem] = []

        for code, f_def in CANONICAL_FIELDS.items():
            field_cands = candidates_by_field[code]

            if not field_cands:
                inv_status = "Missing"
                pref_item = None
            else:
                accepted_cands = [c for c in field_cands if c.candidate_status == "Accepted"]
                if accepted_cands:
                    # Check for genuine conflict among accepted candidates (normalized)
                    unique_acc = {
                        re.sub(r"[\s\.\,\:\;\-\_\"\'“‘”’\(\)\[\]\u200b\u200c\u200d\ufeff]+", "", c.canonical_value.lower())
                        for c in accepted_cands if c.canonical_value
                    }
                    if len(unique_acc) > 1 and code not in ("applicant_name", "divisional_secretariat_division", "project_title"):
                        inv_status = "Needs Review"
                        for c in accepted_cands:
                            c.candidate_status = "Conflict"
                        pref_item = accepted_cands[0]
                    else:
                        accepted_cands.sort(key=lambda c: c.confidence, reverse=True)
                        pref_cand = accepted_cands[0]
                        pref_cand.is_preferred = True
                        pref_cand.rank = 1
                        for rank_idx, oc in enumerate(field_cands, 1):
                            oc.rank = rank_idx
                        preferred_facts.append(pref_cand)
                        inv_status = "Extracted"
                        pref_item = pref_cand
                elif any(c.candidate_status == "Ungrounded" for c in field_cands):
                    inv_status = "Ungrounded"
                    pref_item = field_cands[0]
                elif all(c.candidate_status == "Rejected" for c in field_cands):
                    inv_status = "Rejected"
                    pref_item = field_cands[0]
                else:
                    inv_status = "Needs Review"
                    pref_item = field_cands[0]

            field_inventory.append(
                GovernedFieldInventoryItem(
                    field_code=code,
                    display_name=f_def.display_name,
                    status=inv_status,
                    preferred_value=pref_item.canonical_value if (pref_item and inv_status == "Extracted") else None,
                    raw_value=pref_item.raw_value if pref_item else None,
                    page_number=pref_item.page_number if pref_item else None,
                    confidence=pref_item.confidence if pref_item else None,
                    is_grounded=pref_item.is_evidence_verified if pref_item else False,
                    candidate_count=len(field_cands),
                    source_type=pref_item.source_type if pref_item else None,
                    normalized_english_value=pref_item.normalized_english_value if pref_item else None,
                )
            )

        dur = duration_ms if duration_ms is not None else 0.0

        counts = {
            "candidate_count": len(all_candidates_list),
            "accepted_count": sum(1 for c in all_candidates_list if c.candidate_status == "Accepted"),
            "rejected_count": sum(1 for c in all_candidates_list if c.candidate_status == "Rejected"),
            "conflict_count": sum(1 for c in all_candidates_list if c.candidate_status == "Conflict"),
            "ungrounded_count": sum(1 for c in all_candidates_list if c.candidate_status == "Ungrounded"),
        }

        return SemanticExtractionResult(
            classification=overall_classification,
            facts=preferred_facts,
            all_candidates=all_candidates_list,
            field_inventory=field_inventory,
            candidate_count=counts["candidate_count"],
            accepted_count=counts["accepted_count"],
            rejected_count=counts["rejected_count"],
            conflict_count=counts["conflict_count"],
            ungrounded_count=counts["ungrounded_count"],
            raw_model_response=json.dumps(gemini_response, ensure_ascii=False),
            prompt_version=GEMINI_PROMPT_VERSION,
            model_identifier=self.model_name,
            duration_ms=round(dur, 2),
            warnings=warnings,
        )

    def _build_unavailable_result(
        self,
        start_time: float,
        warnings: List[str],
        error_msg: str,
    ) -> SemanticExtractionResult:
        """Helper building clean safe abstention result when Gemini API fails."""
        duration = (time.perf_counter() - start_time) * 1000.0
        return SemanticExtractionResult(
            classification=SemanticClassificationDto(code="Unavailable", confidence=None),
            facts=[],
            all_candidates=[],
            field_inventory=[
                GovernedFieldInventoryItem(
                    field_code=code,
                    display_name=f_def.display_name,
                    status="Unavailable",
                    preferred_value=None,
                    raw_value=None,
                    page_number=None,
                    confidence=None,
                    is_grounded=False,
                    candidate_count=0,
                    source_type=None,
                    normalized_english_value=None,
                )
                for code, f_def in CANONICAL_FIELDS.items()
            ],
            candidate_count=0,
            accepted_count=0,
            rejected_count=0,
            conflict_count=0,
            ungrounded_count=0,
            raw_model_response="",
            prompt_version=GEMINI_PROMPT_VERSION,
            model_identifier=self.model_name,
            duration_ms=round(duration, 2),
            warnings=warnings,
            is_success=False,
            error_message=error_msg,
        )

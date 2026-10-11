"""Structured output validation, deterministic JSON repair, and catalog enforcement."""

import json
import re
from typing import Any, Dict, List, Optional, Tuple
from app.semantic.catalog import CANONICAL_FIELDS, DOCUMENT_CLASSIFICATION_TAXONOMY


def repair_and_parse_json(raw_text: str) -> Tuple[Optional[Dict[str, Any]], Optional[str]]:
    """
    Attempt to extract and parse valid JSON from raw model output.
    Performs bounded deterministic repairs (stripping markdown fences, trailing commas, preamble).
    
    Returns:
        (parsed_dict, error_message_if_any)
    """
    if not raw_text or not raw_text.strip():
        return None, "Model returned empty response."

    text = raw_text.strip()

    # 1. Direct parse attempt
    try:
        return json.loads(text), None
    except json.JSONDecodeError:
        pass

    # 2. Strip markdown code fences (```json ... ``` or ``` ... ```)
    fence_pattern = r"```(?:json)?\s*([\s\S]*?)\s*```"
    fence_match = re.search(fence_pattern, text, re.IGNORECASE)
    if fence_match:
        fenced_content = fence_match.group(1).strip()
        try:
            return json.loads(fenced_content), None
        except json.JSONDecodeError:
            text = fenced_content

    # 3. Extract substring between first '{' and last '}'
    first_brace = text.find("{")
    last_brace = text.rfind("}")
    if first_brace != -1 and last_brace > first_brace:
        candidate_json = text[first_brace : last_brace + 1]
        try:
            return json.loads(candidate_json), None
        except json.JSONDecodeError:
            # 4. Remove common trailing commas (,\s*}) or (,\s*])
            cleaned = re.sub(r",\s*([}\]])", r"\1", candidate_json)
            try:
                return json.loads(cleaned), None
            except json.JSONDecodeError as ex:
                return None, f"JSON syntax error after repair: {str(ex)}"

    return None, "No valid JSON object could be extracted from model output."


def clamp_confidence(val: Any) -> float:
    """Clamp confidence score to [0.0, 1.0]."""
    try:
        f = float(val)
        if f >= 2.0 and f <= 100.0:
            # Heuristic: model output percentage [2, 100] instead of [0, 1]
            f = f / 100.0
        return max(0.0, min(1.0, round(f, 4)))
    except (ValueError, TypeError):
        return 0.5


def validate_extracted_payload(
    payload: Dict[str, Any],
) -> Tuple[Dict[str, Any], List[str]]:
    """
    Validate and sanitize the parsed dictionary according to catalog and taxonomy.
    
    Returns:
        (sanitized_payload, warnings)
    """
    warnings: List[str] = []

    # 1. Validate Document Classification
    raw_class = payload.get("documentClassification") or {}
    class_code = str(raw_class.get("code") or "Unknown").strip()
    class_conf = clamp_confidence(raw_class.get("confidence", 0.5))

    if class_code not in DOCUMENT_CLASSIFICATION_TAXONOMY:
        warnings.append(
            f"Model suggested classification '{class_code}' which is not in the allowed taxonomy. "
            "Defaulted to 'Other'."
        )
        class_code = "Other"

    classification_result = {
        "code": class_code,
        "confidence": class_conf,
    }

    # 2. Validate Facts
    raw_facts = payload.get("facts") or []
    if not isinstance(raw_facts, list):
        warnings.append("Model 'facts' field was not a list. Treated as empty.")
        raw_facts = []

    valid_facts: List[Dict[str, Any]] = []
    seen_field_codes = set()

    for idx, f in enumerate(raw_facts):
        if not isinstance(f, dict):
            continue

        raw_code = str(f.get("fieldCode") or "").strip()
        raw_value = f.get("value")

        if not raw_code:
            continue

        if raw_code not in CANONICAL_FIELDS:
            warnings.append(
                f"Ignored unknown fact code '{raw_code}' produced by model."
            )
            continue

        if raw_value is None or str(raw_value).strip() == "" or str(raw_value).lower() in ("null", "none", "n/a"):
            continue

        str_val = str(raw_value).strip()
        if len(str_val) > 1000:
            str_val = str_val[:1000]
            warnings.append(f"Fact '{raw_code}' value exceeded 1000 characters and was truncated.")

        conf = clamp_confidence(f.get("confidence", 0.75))

        # Page number validation
        try:
            page_num = int(f.get("pageNumber") or 1)
            if page_num < 1:
                page_num = 1
        except (ValueError, TypeError):
            page_num = 1

        quote = str(f.get("quote") or "").strip()
        if len(quote) > 500:
            quote = quote[:500]

        # Duplicate field handling: retain the first or highest confidence one
        if raw_code in seen_field_codes:
            warnings.append(f"Duplicate candidate fact for '{raw_code}'. Retained highest-confidence occurrence.")
            # Check if current has higher confidence
            existing_idx = next(i for i, ef in enumerate(valid_facts) if ef["fieldCode"] == raw_code)
            if conf > valid_facts[existing_idx]["confidence"]:
                valid_facts[existing_idx] = {
                    "fieldCode": raw_code,
                    "valueKind": CANONICAL_FIELDS[raw_code].value_kind,
                    "canonicalValue": str_val,
                    "confidence": conf,
                    "pageNumber": page_num,
                    "quote": quote,
                }
            continue

        seen_field_codes.add(raw_code)
        valid_facts.append({
            "fieldCode": raw_code,
            "valueKind": CANONICAL_FIELDS[raw_code].value_kind,
            "canonicalValue": str_val,
            "confidence": conf,
            "pageNumber": page_num,
            "quote": quote,
        })

    # Preserve any incoming warnings
    incoming_warnings = payload.get("warnings") or []
    if isinstance(incoming_warnings, list):
        for w in incoming_warnings:
            if isinstance(w, str) and w.strip():
                warnings.append(w.strip())

    return {
        "documentClassification": classification_result,
        "facts": valid_facts,
    }, warnings

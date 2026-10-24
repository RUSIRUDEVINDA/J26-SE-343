"""Structured output validation, deterministic JSON repair, and catalog enforcement."""

import json
import re
from typing import Any, Dict, List, Optional, Tuple

from app.semantic.catalog import CANONICAL_FIELDS, DOCUMENT_CLASSIFICATION_TAXONOMY
from app.semantic.field_validators import (
    PURPOSE_ACTION_PATTERN_EN,
    PURPOSE_ACTION_PATTERN_SIN,
    execute_cross_field_validation,
    is_officer_or_staff_designation,
    validate_candidate_field,
)


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
            except json.JSONDecodeError:
                # 5. If truncated mid-item inside an array, close at the last completed object
                if "facts" in candidate_json and "[" in candidate_json:
                    last_inner_brace = candidate_json.rfind("}")
                    if last_inner_brace != -1:
                        repaired_arr = candidate_json[:last_inner_brace + 1].rstrip(", \n\r") + "\n  ]\n}"
                        try:
                            return json.loads(repaired_arr), None
                        except json.JSONDecodeError:
                            pass
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
    Validate, sanitize, and semantically evaluate the parsed dictionary according to catalog and taxonomy.
    Preserves all extracted candidate facts (without silently discarding candidates).
    
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

    processed_facts: List[Dict[str, Any]] = []

    for f in raw_facts:
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

        if raw_value is None or str(raw_value).strip() == "" or str(raw_value).lower() in ("null", "none", "n/a", "-"):
            # Abstention / missing fact - do not produce empty candidate
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

        # Field-specific deterministic semantic validation
        field_val_result = validate_candidate_field(
            field_code=raw_code,
            value=str_val,
            quote=quote,
            page_number=page_num,
        )

        val_status = "Valid" if field_val_result.is_valid else "Invalid"
        if not field_val_result.is_valid:
            warnings.extend([f"[{raw_code}] {r}" for r in field_val_result.reasons])

        canonical_val = field_val_result.normalized_value if field_val_result.normalized_value else str_val

        fact_dict = {
            "fieldCode": raw_code,
            "valueKind": CANONICAL_FIELDS[raw_code].value_kind,
            "canonicalValue": canonical_val,
            "rawValue": str_val,
            "normalizedValue": field_val_result.normalized_value,
            "confidence": conf,
            "pageNumber": page_num,
            "quote": quote,
            "isSemanticValid": field_val_result.is_valid,
            "validationStatus": val_status,
            "validationReasons": field_val_result.reasons,
            "candidateStatus": field_val_result.status,
        }

        # Handle duplicates within this single payload dictionary
        existing_idx = next((i for i, vf in enumerate(processed_facts) if vf["fieldCode"] == raw_code), None)
        if existing_idx is not None:
            if conf > processed_facts[existing_idx]["confidence"]:
                warnings.append(
                    f"Duplicate candidate fact for field '{raw_code}' in chunk payload. Selected candidate with higher confidence."
                )
                processed_facts[existing_idx] = fact_dict
            else:
                warnings.append(
                    f"Duplicate candidate fact for field '{raw_code}' in chunk payload. Ignored candidate with lower confidence."
                )
            continue

        processed_facts.append(fact_dict)

    # Cross-field consistency checks
    cross_warnings = execute_cross_field_validation(processed_facts)
    warnings.extend(cross_warnings)

    # Preserve any incoming warnings
    incoming_warnings = payload.get("warnings") or []
    if isinstance(incoming_warnings, list):
        for w in incoming_warnings:
            if isinstance(w, str) and w.strip():
                warnings.append(w.strip())

    return {
        "documentClassification": classification_result,
        "facts": processed_facts,
    }, warnings


def resolve_and_inventory_candidates(
    all_candidates: List[Any],
) -> Tuple[List[Any], List[Dict[str, Any]], Dict[str, int], List[str]]:
    """
    Given all extracted and grounded candidates:
    1. Deterministically ranks and resolves preferred candidates per field.
    2. Identifies singular vs multi-value field conflicts.
    3. Builds the complete 17-field catalog inventory.
    4. Computes candidate counts (candidate_count, accepted_count, rejected_count, conflict_count, ungrounded_count).

    Returns:
        (preferred_facts, field_inventory, counts, additional_warnings)
    """
    multi_valued_fields = {"supporting_document_references", "legal_statutory_basis"}
    warnings: List[str] = []

    # Deterministic cross-field validation on candidate set
    title_cands = [
        c for c in all_candidates
        if c.field_code == "project_title" and c.is_semantic_valid and c.candidate_status != "Rejected"
    ]
    applicant_cands = [c for c in all_candidates if c.field_code == "applicant_name"]
    for ac in applicant_cands:
        for tc in title_cands:
            if ac.canonical_value.strip().lower() == tc.canonical_value.strip().lower():
                ac.is_semantic_valid = False
                ac.candidate_status = "Rejected"
                reasons = list(ac.validation_reasons or [])
                reasons.append("Applicant name is identical to project title; cannot be accepted without explicit legal entity evidence.")
                ac.validation_reasons = reasons
                warnings.append(
                    f"Rejected applicant_name candidate '{ac.canonical_value}' because it is identical to project_title."
                )

    # 2. Cross-field: Supporting document references != project title
    supp_cands = [c for c in all_candidates if c.field_code == "supporting_document_references"]
    for sc in supp_cands:
        s_val = sc.canonical_value.strip().lower()
        s_quote = getattr(sc, "quote", "") or ""
        for tc in title_cands:
            t_val = tc.canonical_value.strip().lower()
            if s_val and t_val and (s_val == t_val or s_val in t_val):
                if not re.search(r"(?:attached|enclosed|annex|annexure|attachment|schedule|reference|appendix|ඇමුණුම|අමුණා\s*ඇත)", s_quote, re.IGNORECASE):
                    sc.is_semantic_valid = False
                    sc.candidate_status = "Rejected"
                    reasons = list(sc.validation_reasons or [])
                    reasons.append("Supporting document reference matches project title and lacks explicit attachment or reference context.")
                    sc.validation_reasons = reasons
                    warnings.append(
                        f"Rejected supporting_document_references candidate '{sc.canonical_value}' "
                        "because it matches project_title without attachment evidence."
                    )

    # 3. Cross-field: Land Extent + Unit Companion Validation
    extent_cands = [c for c in all_candidates if c.field_code == "land_extent"]
    unit_cands = [
        c for c in all_candidates
        if c.field_code == "land_extent_unit" and c.is_semantic_valid and c.is_evidence_verified and c.candidate_status == "Accepted"
    ]
    for ec in extent_cands:
        val = ec.canonical_value.strip()
        quote = getattr(ec, "quote", "") or ""
        is_bare = bool(re.match(r"^\d+(?:\.\d+)?$", val))
        has_quote_unit = bool(re.search(r"(?:acre|hectare|ha|perch|rood|අක්කර|හෙක්ටයාර්|පර්චස්|රූඩ්)", quote, re.IGNORECASE))

        if is_bare and not has_quote_unit:
            # Check for grounded, accepted companion unit from the same page / source context
            matching_unit = next((uc for uc in unit_cands if uc.page_number == ec.page_number), None)
            if matching_unit and ec.is_evidence_verified:
                ec.is_semantic_valid = True
                ec.candidate_status = "Accepted"
                ec.validation_reasons = [
                    f"Accepted using grounded companion land_extent_unit '{matching_unit.canonical_value}' from the same source context."
                ]
                warnings.append(
                    f"Accepted land_extent candidate '{val}' using companion unit '{matching_unit.canonical_value}' on page {ec.page_number}."
                )
            else:
                ec.is_semantic_valid = False
                ec.candidate_status = "Rejected"
                reasons = list(ec.validation_reasons or [])
                reasons.append(
                    f"Value '{val}' is a bare number lacking recognized land area units or A/R/P notation, "
                    f"and no grounded companion land_extent_unit exists on page {ec.page_number}."
                )
                ec.validation_reasons = reasons
                warnings.append(
                    f"Rejected bare land_extent candidate '{val}' on page {ec.page_number} due to lack of companion area unit."
                )

    # 4. Cross-field: Investment Currency binding check
    inv_cands = [c for c in all_candidates if c.field_code == "investment_amount" and c.is_semantic_valid and c.is_evidence_verified]
    curr_cands = [c for c in all_candidates if c.field_code == "investment_currency" and c.is_semantic_valid]
    if curr_cands and inv_cands:
        for cc in curr_cands:
            shares_context = any(
                ic.page_number == cc.page_number or
                cc.canonical_value.lower() in (getattr(ic, "quote", "") or "").lower() or
                cc.canonical_value.lower() in ic.canonical_value.lower()
                for ic in inv_cands
            )
            if not shares_context:
                cc.candidate_status = "NeedsReview"
                reasons = list(cc.validation_reasons or [])
                reasons.append("Investment currency candidate does not share source context with extracted investment_amount.")
                cc.validation_reasons = reasons

    # 5. Cross-field: Land Location must not be officer designation or applicant name
    loc_cands = [c for c in all_candidates if c.field_code == "land_location"]
    for lc in loc_cands:
        l_val = lc.canonical_value.strip()
        if is_officer_or_staff_designation(l_val):
            lc.is_semantic_valid = False
            lc.candidate_status = "Rejected"
            reasons = list(lc.validation_reasons or [])
            reasons.append("Land location candidate matches an official designation or staff title.")
            lc.validation_reasons = reasons
            warnings.append(f"Rejected land_location candidate '{l_val}' because it matches an officer designation.")
        for ac in applicant_cands:
            a_val = ac.canonical_value.strip().lower()
            if l_val.lower() == a_val:
                lc.is_semantic_valid = False
                lc.candidate_status = "Rejected"
                reasons = list(lc.validation_reasons or [])
                reasons.append("Land location candidate is identical to applicant name.")
                lc.validation_reasons = reasons
                warnings.append(f"Rejected land_location candidate '{l_val}' because it matches applicant_name.")

    # 6. Cross-field: Project Purpose must not be pure district or DSD without action semantics
    purpose_cands = [c for c in all_candidates if c.field_code == "project_purpose"]
    dist_cands = [c for c in all_candidates if c.field_code == "district" and c.is_semantic_valid]
    dsd_cands = [c for c in all_candidates if c.field_code == "divisional_secretariat_division" and c.is_semantic_valid]
    for pc in purpose_cands:
        p_val = pc.canonical_value.strip()
        has_action = bool(PURPOSE_ACTION_PATTERN_EN.search(p_val) or PURPOSE_ACTION_PATTERN_SIN.search(p_val))
        if not has_action:
            matches_admin = any(
                p_val.lower() == dc.canonical_value.strip().lower() for dc in dist_cands
            ) or any(
                p_val.lower() == dsc.canonical_value.strip().lower() for dsc in dsd_cands
            )
            if matches_admin:
                pc.is_semantic_valid = False
                pc.candidate_status = "Rejected"
                reasons = list(pc.validation_reasons or [])
                reasons.append("Project purpose candidate matches an administrative division (district/DSD) without action/purpose semantics.")
                pc.validation_reasons = reasons
                warnings.append(f"Rejected project_purpose candidate '{p_val}' because it matches an administrative division without purpose semantics.")

    candidates_by_field: Dict[str, List[Any]] = {}
    for c in all_candidates:
        candidates_by_field.setdefault(c.field_code, []).append(c)

    preferred_facts: List[Any] = []

    for field_code, field_cands in candidates_by_field.items():
        if field_code in multi_valued_fields:
            # Multi-valued fields: retain valid and grounded candidates
            for idx, cand in enumerate(field_cands):
                cand.rank = idx + 1
                if cand.is_semantic_valid and cand.is_evidence_verified:
                    if cand.candidate_status == "NeedsReview":
                        cand.is_preferred = False
                    else:
                        cand.candidate_status = "Accepted"
                        cand.is_preferred = True
                        preferred_facts.append(cand)
                elif not cand.is_evidence_verified:
                    cand.candidate_status = "Ungrounded"
                    cand.is_preferred = False
                else:
                    cand.candidate_status = "Rejected"
                    cand.is_preferred = False
            continue

        # Singular field candidate ranking
        def candidate_sort_key(c: Any) -> Tuple[int, int, int, float]:
            # Priority 1: Semantic validity and non-rejected status
            valid_score = 1 if c.is_semantic_valid and c.candidate_status not in ("Rejected", "Ungrounded") else 0
            # Priority 2: Verbatim evidence grounding
            ground_score = 1 if c.is_evidence_verified else 0
            # Priority 3: Soft source page prior (ranking bonus only, never invalidates)
            page_score = 0
            if c.field_code == "project_title":
                page_score = 2 if c.page_number == 1 else (1 if c.page_number <= 3 else 0)
            elif c.field_code in ("applicant_name", "applicant_type"):
                page_score = 1 if c.page_number <= 3 else 0
            # Priority 4: Model confidence
            conf_score = c.confidence or 0.0
            return (valid_score, ground_score, page_score, conf_score)

        field_cands.sort(key=candidate_sort_key, reverse=True)

        for idx, cand in enumerate(field_cands):
            cand.rank = idx + 1
            cand.is_preferred = False

        top = field_cands[0]
        if top.is_semantic_valid and top.is_evidence_verified:
            if top.candidate_status == "NeedsReview":
                # Do not promote NeedsReview to Accepted; retain as non-preferred candidate for human review
                top.is_preferred = False
            else:
                # Check for conflict among other valid and grounded candidates
                conflicting_cands = [
                    c for c in field_cands[1:]
                    if c.is_semantic_valid and c.is_evidence_verified
                    and c.candidate_status not in ("Rejected", "NeedsReview")
                    and c.canonical_value.strip().lower() != top.canonical_value.strip().lower()
                ]

                if conflicting_cands:
                    # Soft page prior helps resolve if top is on page 1 for title
                    top_page_priority = (top.page_number == 1) if field_code == "project_title" else False
                    if top_page_priority:
                        top.is_preferred = True
                        top.candidate_status = "Accepted"
                        preferred_facts.append(top)
                        for c in conflicting_cands:
                            c.candidate_status = "NeedsReview"
                    else:
                        for c in [top] + conflicting_cands:
                            c.candidate_status = "Conflict"
                            c.is_preferred = False
                            preferred_facts.append(c)
                        warnings.append(
                            f"Conflicting candidates extracted for field '{field_code}': "
                            f"{[top.canonical_value] + [c.canonical_value for c in conflicting_cands]}. "
                            "Retaining candidates for human verification."
                        )
                else:
                    top.is_preferred = True
                    top.candidate_status = "Accepted"
                    preferred_facts.append(top)
        elif not top.is_evidence_verified:
            top.candidate_status = "Ungrounded"
            top.is_preferred = False
        else:
            top.candidate_status = "Rejected"
            top.is_preferred = False

    # Build 17-field catalog inventory
    field_inventory: List[Dict[str, Any]] = []
    for code, field_def in CANONICAL_FIELDS.items():
        fcands = candidates_by_field.get(code, [])
        pref = next((c for c in fcands if c.is_preferred), None)

        if not fcands:
            inv_status = "Missing"
        elif pref:
            inv_status = "Extracted"
        elif all(c.candidate_status == "Rejected" for c in fcands):
            inv_status = "Rejected"
        elif any(c.candidate_status == "Conflict" for c in fcands):
            inv_status = "Conflict"
        elif any(c.candidate_status == "Ungrounded" for c in fcands):
            inv_status = "Ungrounded"
        else:
            inv_status = "Needs Review"

        field_inventory.append({
            "field_code": code,
            "display_name": field_def.display_name,
            "status": inv_status,
            "preferred_value": pref.canonical_value if pref else None,
            "raw_value": fcands[0].raw_value if fcands else None,
            "page_number": pref.page_number if pref else (fcands[0].page_number if fcands else None),
            "confidence": pref.confidence if pref else (fcands[0].confidence if fcands else None),
            "is_grounded": pref.is_evidence_verified if pref else False,
            "candidate_count": len(fcands),
        })

    counts = {
        "candidate_count": len(all_candidates),
        "accepted_count": sum(1 for c in all_candidates if getattr(c, "candidate_status", "") == "Accepted"),
        "rejected_count": sum(1 for c in all_candidates if getattr(c, "candidate_status", "") == "Rejected"),
        "conflict_count": sum(1 for c in all_candidates if getattr(c, "candidate_status", "") == "Conflict"),
        "ungrounded_count": sum(1 for c in all_candidates if getattr(c, "candidate_status", "") == "Ungrounded"),
    }

    return preferred_facts, field_inventory, counts, warnings

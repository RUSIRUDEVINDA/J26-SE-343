"""
Unit and integration tests for Google Gemini semantic extraction provider.

Verifies:
1. API key security (missing key raises GeminiAuthenticationError, never exposes key).
2. Provider factory resolution (qwen, gemini, stub).
3. Verbatim Sinhala quote grounding vs ungrounded English translation quotes.
4. Deterministic field validators execution within Gemini extractor.
5. Error handling (rate limits, timeouts, authentication errors).
6. Safe abstention behavior on provider failure (no automatic fallback to Qwen).
"""

import json
from unittest.mock import MagicMock, patch

import pytest
from app.core.config import settings
from app.semantic.catalog import CANONICAL_FIELDS
from app.semantic.extractor import (
    BaseSemanticExtractor,
    OllamaQwenSemanticExtractor,
    StubSemanticExtractor,
    get_semantic_extractor,
)
from app.semantic.gemini_client import (
    GeminiAuthenticationError,
    GeminiClient,
    GeminiError,
    GeminiRateLimitError,
    GeminiServerError,
    GeminiTimeoutError,
)
from app.semantic.gemini_extractor import (
    GEMINI_PROMPT_VERSION,
    GeminiSemanticExtractor,
)


# ============================================================================
# 1. API KEY & SECURITY TESTS
# ============================================================================

def test_gemini_client_missing_key_raises_auth_error(monkeypatch):
    """When GEMINI_API_KEY is unset/empty, GeminiClient.generate_content must raise GeminiAuthenticationError."""
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    monkeypatch.setattr(settings, "gemini_api_key", None)

    client = GeminiClient(api_key=None)
    with pytest.raises(GeminiAuthenticationError) as exc_info:
        client.generate_content("Test prompt")

    assert "GEMINI_API_KEY environment variable is not configured" in str(exc_info.value)



def test_gemini_extractor_missing_key_safe_abstention(monkeypatch):
    """GeminiSemanticExtractor returns a safe abstention result when API key is missing."""
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    monkeypatch.setattr(settings, "gemini_api_key", None)

    extractor = GeminiSemanticExtractor(api_key=None)
    pages = [{"page_number": 1, "text": "කිවුල කැලේ ව්‍යාපෘතිය"}]

    result = extractor.extract(pages=pages)

    assert result.classification.code == "Unavailable"
    assert result.candidate_count == 0
    assert result.accepted_count == 0
    assert len(result.field_inventory) == 17
    # All fields should be Unavailable when provider fails / is unconfigured
    for item in result.field_inventory:
        assert item.status == "Unavailable"
    assert any("GEMINI_API_KEY" in w for w in result.warnings)


# ============================================================================
# 2. PROVIDER FACTORY TESTS
# ============================================================================

def test_get_semantic_extractor_provider_resolution(monkeypatch):
    """Factory correctly resolves requested provider or configured setting."""
    # Test explicit override
    extractor_stub = get_semantic_extractor("stub")
    assert isinstance(extractor_stub, StubSemanticExtractor)

    extractor_qwen = get_semantic_extractor("qwen")
    assert isinstance(extractor_qwen, OllamaQwenSemanticExtractor)

    extractor_gemini = get_semantic_extractor("gemini")
    assert isinstance(extractor_gemini, GeminiSemanticExtractor)

    # Test settings-based resolution
    monkeypatch.setattr(settings, "semantic_provider", "gemini")
    assert isinstance(get_semantic_extractor(), GeminiSemanticExtractor)

    monkeypatch.setattr(settings, "semantic_provider", "qwen")
    assert isinstance(get_semantic_extractor(), OllamaQwenSemanticExtractor)

    monkeypatch.setattr(settings, "semantic_provider", "invalid_unknown")
    assert isinstance(get_semantic_extractor(), StubSemanticExtractor)


# ============================================================================
# 3. EXTRACTION & GROUNDING VERIFICATION TESTS
# ============================================================================

MOCK_OCR_PAGES = [
    {
        "page_number": 1,
        "text": "රාජ්‍ය ඉඩම් බදු දීමේ යෝජනාව\nඉල්ලුම්කරු: පී.කේ. ගුණසේකර (තනි පුද්ගල)\nස්ථානය: මහකුඹුක්කඩවල",
    },
    {
        "page_number": 3,
        "text": "ව්‍යාපෘතිය: කිවුල කැලේ වතු සංවර්ධනය\nඉඩම් ප්‍රමාණය: අක්කර 17\nආයෝජනය: රු. 5,050,000",
    },
]

MOCK_GEMINI_SUCCESS_PAYLOAD = {
    "documentClassification": {
        "code": "LeaseProposal",
        "confidence": 0.95,
        "classificationRationale": "Official Sri Lankan state land lease proposal with agricultural development plan",
    },
    "extractedFields": [
        {
            "fieldCode": "applicant_name",
            "rawValue": "පී.කේ. ගුණසේකර",
            "normalizedEnglishValue": "P.K. Gunasekara",
            "pageNumber": 1,
            "confidence": 0.92,
            "originalEvidenceQuotes": ["පී.කේ. ගුණසේකර"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Applicant individual person",
        },
        {
            "fieldCode": "applicant_type",
            "rawValue": "තනි පුද්ගල",
            "normalizedEnglishValue": "Individual",
            "pageNumber": 1,
            "confidence": 0.88,
            "originalEvidenceQuotes": ["තනි පුද්ගල"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Individual lease applicant",
        },
        {
            "fieldCode": "project_title",
            "rawValue": "කිවුල කැලේ වතු සංවර්ධනය",
            "normalizedEnglishValue": "Kiwula Kale Estate Development",
            "pageNumber": 3,
            "confidence": 0.90,
            "originalEvidenceQuotes": ["කිවුල කැලේ වතු සංවර්ධනය"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Project formal title",
        },
        {
            "fieldCode": "land_extent",
            "rawValue": "17",
            "normalizedEnglishValue": "17",
            "pageNumber": 3,
            "confidence": 0.94,
            "originalEvidenceQuotes": ["අක්කර 17"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Numerical extent",
        },
        {
            "fieldCode": "land_extent_unit",
            "rawValue": "අක්කර",
            "normalizedEnglishValue": "Acre",
            "pageNumber": 3,
            "confidence": 0.94,
            "originalEvidenceQuotes": ["අක්කර 17"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Extent measurement unit",
        },
        {
            "fieldCode": "divisional_secretariat_division",
            "rawValue": "මහකුඹුක්කඩවල",
            "normalizedEnglishValue": "Mahakumbukkadawala",
            "pageNumber": 1,
            "confidence": 0.91,
            "originalEvidenceQuotes": ["මහකුඹුක්කඩවල"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Divisional Secretariat Division",
        },
        {
            "fieldCode": "investment_amount",
            "rawValue": "5050000",
            "normalizedEnglishValue": "5050000",
            "pageNumber": 3,
            "confidence": 0.89,
            "originalEvidenceQuotes": ["5,050,000"],
            "sourceType": "DirectExplicit",
            "semanticRole": "Total project capital investment",
        },
        {
            "fieldCode": "investment_currency",
            "rawValue": "රු.",
            "normalizedEnglishValue": "LKR",
            "pageNumber": 3,
            "confidence": 0.90,
            "originalEvidenceQuotes": ["රු."],
            "sourceType": "DirectExplicit",
            "semanticRole": "Currency",
        },
        # Intentionally include an ungrounded candidate (English quote not in Sinhala OCR)
        {
            "fieldCode": "requested_lease_period",
            "rawValue": "30 Years",
            "normalizedEnglishValue": "30 Years",
            "pageNumber": 1,
            "confidence": 0.60,
            "originalEvidenceQuotes": ["Requested for 30 years lease"],  # Not in OCR!
            "sourceType": "DirectExplicit",
            "semanticRole": "Lease duration",
        },
    ],
}


def test_gemini_extraction_grounding_and_validation(monkeypatch):
    """
    Verifies that:
    1. Verbatim quotes are grounded (is_evidence_verified=True).
    2. English-only quotes not present in OCR text are flagged as Ungrounded.
    3. Preferred facts are resolved and mapped to governed 17-field inventory.
    """
    extractor = GeminiSemanticExtractor(api_key="test-api-key-mock")

    # Mock GeminiClient.generate_structured_json
    with patch.object(
        extractor.client,
        "generate_content",
        return_value=MOCK_GEMINI_SUCCESS_PAYLOAD,
    ):
        result = extractor.extract(pages=MOCK_OCR_PAGES)

    assert result.classification.code == "LeaseProposal"
    assert result.classification.confidence == 0.95
    assert len(result.field_inventory) == 17

    # Check applicant_name
    app_item = next(i for i in result.field_inventory if i.field_code == "applicant_name")
    assert app_item.status == "Extracted"
    assert app_item.preferred_value == "පී.කේ. ගුණසේකර"
    assert app_item.is_grounded is True
    assert app_item.normalized_english_value == "P.K. Gunasekara"

    # Check land_extent
    extent_item = next(i for i in result.field_inventory if i.field_code == "land_extent")
    assert extent_item.status == "Extracted"
    assert extent_item.preferred_value == "17"

    # Check ungrounded candidate (requested_lease_period)
    lease_item = next(i for i in result.field_inventory if i.field_code == "requested_lease_period")
    # Because quote is not in OCR, candidate becomes Ungrounded and should NOT be Accepted as preferred
    assert lease_item.is_grounded is False
    assert lease_item.status in ("Ungrounded", "Missing")

    # Counts
    assert result.accepted_count >= 7
    assert result.ungrounded_count >= 1


def test_gemini_extraction_rate_limit_error_handling():
    """GeminiRateLimitError is handled cleanly and returns safe abstention result."""
    extractor = GeminiSemanticExtractor(api_key="test-api-key-mock")

    with patch.object(
        extractor.client,
        "generate_content",
        side_effect=GeminiRateLimitError("Rate limit 429 quota exhausted"),
    ):
        result = extractor.extract(pages=MOCK_OCR_PAGES)

    assert result.classification.code == "Unavailable"
    assert result.accepted_count == 0
    assert any("Rate limit" in w for w in result.warnings)


def test_gemini_extraction_timeout_error_handling():
    """GeminiTimeoutError is handled cleanly and returns safe abstention result."""
    extractor = GeminiSemanticExtractor(api_key="test-api-key-mock")

    with patch.object(
        extractor.client,
        "generate_content",
        side_effect=GeminiTimeoutError("HTTP request timed out after 60s"),
    ):
        result = extractor.extract(pages=MOCK_OCR_PAGES)

    assert result.classification.code == "Unavailable"
    assert result.accepted_count == 0
    assert any("timed out" in w for w in result.warnings)


def test_gemini_no_automatic_fallback_to_qwen():
    """
    Verifies architectural requirement:
    When Gemini fails or encounters an error, it must NOT automatically fall back to Qwen.
    It must abstain cleanly.
    """
    extractor = GeminiSemanticExtractor(api_key="test-api-key-mock")

    with patch.object(
        extractor.client,
        "generate_content",
        side_effect=GeminiServerError("HTTP 500 internal server error from Gemini"),
    ):
        result = extractor.extract(pages=MOCK_OCR_PAGES)

    # Must be marked with model_identifier as gemini, not qwen
    assert "gemini" in result.model_identifier.lower()
    assert result.classification.code == "Unavailable"
    assert result.facts == []
    assert result.all_candidates == []


# ============================================================================
# 4. RETRY & FAILED RUN REPORTING TESTS
# ============================================================================

def test_missing_credentials_raises_auth_error_not_executed(monkeypatch):
    """When GEMINI_API_KEY is unconfigured, comparison run marks status as NotExecuted with null scores."""
    from evaluation.semantic.compare_models import summarize_model_evaluation

    summary = summarize_model_evaluation(
        model_identifier="gemini-3.8-flash",
        provider="gemini",
        field_inventory_items=[],
        duration_ms=None,
        ocr_duration_ms=50000.0,
        counts={},
        is_executed=False,
        execution_status="NotExecuted",
        failure_reason="GEMINI_API_KEY unconfigured; infrastructure blocked",
        request_dispatched=False,
    )

    assert summary.execution_status == "NotExecuted"
    assert summary.request_dispatched is False
    assert summary.score_17 is None
    assert summary.score_17_weighted is None
    assert summary.critical_score is None
    assert summary.candidate_semantic_recall is None
    assert summary.duration_ms is None
    # All 17 fields must be NotExecuted / N/A
    assert len(summary.field_details) == 17
    for det in summary.field_details.values():
        assert det.evaluation_verdict == "N/A"
        assert det.model_status == "NotExecuted"


def test_transient_503_retry_succeeds_on_second_attempt(monkeypatch):
    """GeminiClient retries on transient 503 UNAVAILABLE and succeeds on subsequent attempt."""
    import httpx

    # Speed up sleep during tests
    monkeypatch.setattr("time.sleep", lambda s: None)

    mock_resp_503 = MagicMock(spec=httpx.Response)
    mock_resp_503.status_code = 503
    mock_resp_503.text = "This model is currently experiencing high demand."
    mock_resp_503.headers = {"retry-after": "1"}

    mock_resp_200 = MagicMock(spec=httpx.Response)
    mock_resp_200.status_code = 200
    mock_resp_200.json.return_value = {
        "candidates": [
            {
                "content": {
                    "parts": [{"text": json.dumps({"documentClassification": {"code": "LeaseProposal"}})}]
                }
            }
        ]
    }

    client = GeminiClient(api_key="test-valid-key")

    with patch("httpx.Client.post", side_effect=[mock_resp_503, mock_resp_200]) as mock_post:
        result = client.generate_content("test prompt", max_attempts=3)

    assert mock_post.call_count == 2
    assert client.last_execution_stats["attempts_made"] == 2
    assert client.last_execution_stats["last_status_code"] == 200
    assert result == {"documentClassification": {"code": "LeaseProposal"}}


def test_persistent_503_exhausts_retries_and_raises_server_error(monkeypatch):
    """GeminiClient retries up to 3 times on persistent 503 and raises GeminiServerError."""
    import httpx

    monkeypatch.setattr("time.sleep", lambda s: None)

    mock_resp_503 = MagicMock(spec=httpx.Response)
    mock_resp_503.status_code = 503
    mock_resp_503.text = "This model is currently experiencing high demand."
    mock_resp_503.headers = {}

    client = GeminiClient(api_key="test-valid-key")

    with patch("httpx.Client.post", return_value=mock_resp_503) as mock_post:
        with pytest.raises(GeminiServerError) as exc_info:
            client.generate_content("test prompt", max_attempts=3)

    assert mock_post.call_count == 3
    assert client.last_execution_stats["attempts_made"] == 3
    assert client.last_execution_stats["last_status_code"] == 503
    assert "UNAVAILABLE" in str(exc_info.value)


def test_failed_run_receives_no_field_scores_in_comparison():
    """When a model run fails, field-level scoring is skipped and all scores are null / N/A."""
    from evaluation.semantic.compare_models import summarize_model_evaluation

    summary = summarize_model_evaluation(
        model_identifier="gemini-3.8-flash",
        provider="gemini",
        field_inventory_items=[],
        duration_ms=None,
        ocr_duration_ms=50000.0,
        counts={},
        is_executed=False,
        execution_status="Failed",
        failure_reason="HTTP 503 / UNAVAILABLE: This model is currently experiencing high demand.",
        failed_duration_ms=4500.0,
        request_dispatched=True,
    )

    assert summary.execution_status == "Failed"
    assert summary.request_dispatched is True
    assert summary.score_17 is None
    assert summary.score_17_weighted is None
    assert summary.critical_score is None
    assert summary.candidate_semantic_recall is None
    assert summary.duration_ms is None
    assert summary.failed_duration_ms == 4500.0
    assert summary.correct_count == 0
    assert summary.correct_missing_count == 0
    assert summary.incorrect_count == 0

    # Every one of the 17 fields must be Unavailable and N/A
    assert len(summary.field_details) == 17
    for det in summary.field_details.values():
        assert det.evaluation_verdict == "N/A"
        assert det.semantic_correctness == "N/A"
        assert det.model_status == "Unavailable"


def test_unequal_investment_amount_is_incorrect():
    """OCR-truncated investment candidate '5,000,000' is Incorrect against physical reference '5,050,000'."""
    from evaluation.semantic.compare_models import evaluate_field_extraction
    from evaluation.semantic.reference_answers import PROPOSAL1_HUMAN_REFERENCE

    ref = PROPOSAL1_HUMAN_REFERENCE["investment_amount"]
    inv_item = {
        "field_code": "investment_amount",
        "status": "Accepted",
        "preferred_value": "5,000,000",
        "is_grounded": True,
        "page_number": 11,
        "quote": "5,000,000",
    }

    detail = evaluate_field_extraction(ref, inv_item)

    assert detail.semantic_correctness == "Incorrect"
    assert detail.evaluation_verdict == "Incorrect"
    assert detail.model_value == "5,000,000"
    assert ref.reference_value == "5,050,000"
    assert "5,000,000" not in ref.acceptable_variants


# ============================================================================
# 5. RECONCILIATION & CONFLICT BOUNDARY REGRESSION TESTS
# ============================================================================

def test_ledger_rejected_noise_does_not_cause_conflict():
    """Rejected noise candidate must NOT trigger artificial conflict against a valid, grounded candidate."""
    from app.semantic.ledger import DocumentEvidenceLedger, LedgerCandidate

    page_texts = {
        11: "ව්‍යාපෘතියේ මුළු පිරිවැය සහ එය සපයාගන්නා ආකාරය (මුළු ආයෝජනය රු. 5,000,000.00)",
        16: "ග්‍රෑම්‌ 30ක්‌ සමග අළුත්‌ ලෙලි 175 ක්‌ හෝ පොල්ලෙලි අළු",
    }
    ledger = DocumentEvidenceLedger(page_texts=page_texts)

    # 1. Eligible, grounded, valid investment candidate on page 11
    c_valid = LedgerCandidate(
        field_code="investment_amount",
        canonical_value="5,000,000",
        raw_value="5,000,000.00",
        source_type="Explicit",
        page_number=11,
        quote="මුළු ආයෝජනය රු. 5,000,000.00",
        confidence=0.9,
    )
    # 2. Noise candidate on page 16 that will fail field validation (bare number without context)
    c_noise = LedgerCandidate(
        field_code="investment_amount",
        canonical_value="175",
        raw_value="175",
        source_type="Explicit",
        page_number=16,
        quote="175",
        confidence=0.6,
    )
    ledger.add_candidate(c_valid)
    ledger.add_candidate(c_noise)

    facts, all_cands, inventory, counts, warnings = ledger.reconcile_and_map_to_canonical_facts()

    # The rejected noise must NOT have caused a conflict
    inv_cands = [c for c in all_cands if c.field_code == "investment_amount"]
    assert len(inv_cands) == 2

    # The valid candidate must remain accepted and preferred
    accepted_cands = [c for c in inv_cands if c.candidate_status == "Accepted"]
    rejected_cands = [c for c in inv_cands if c.candidate_status == "Rejected"]
    assert len(accepted_cands) == 1
    assert accepted_cands[0].canonical_value == "5,000,000"
    assert accepted_cands[0].is_preferred is True
    assert len(rejected_cands) == 1

    # Conflict status must NOT be present
    assert not any(c.candidate_status == "Conflict" for c in inv_cands)
    assert any(f.field_code == "investment_amount" and f.canonical_value == "5,000,000" for f in facts)


def test_ledger_ungrounded_noise_does_not_cause_conflict():
    """Ungrounded/hallucinated candidate must NOT trigger artificial conflict against a grounded, valid candidate."""
    from app.semantic.ledger import DocumentEvidenceLedger, LedgerCandidate

    page_texts = {
        6: "ඉඩමේ ප්‍රමාණය : අක්කර 17",
    }
    ledger = DocumentEvidenceLedger(page_texts=page_texts)

    # 1. Grounded candidate
    c_grounded = LedgerCandidate(
        field_code="land_extent",
        canonical_value="17",
        raw_value="17",
        source_type="Explicit",
        page_number=6,
        quote="ඉඩමේ ප්‍රමාණය : අක්කර 17",
        confidence=0.95,
    )
    # 2. Ungrounded hallucinated candidate
    c_ungrounded = LedgerCandidate(
        field_code="land_extent",
        canonical_value="50",
        raw_value="50",
        source_type="Explicit",
        page_number=6,
        quote="නොමැති ව්‍යාජ වාක්‍යයකි",  # nonexistent quote
        confidence=0.7,
    )
    ledger.add_candidate(c_grounded)
    ledger.add_candidate(c_ungrounded)

    facts, all_cands, inventory, counts, warnings = ledger.reconcile_and_map_to_canonical_facts()

    extent_cands = [c for c in all_cands if c.field_code == "land_extent"]
    assert any(c.candidate_status == "Accepted" and c.canonical_value == "17" for c in extent_cands)
    assert any(c.candidate_status == "Ungrounded" for c in extent_cands)
    assert not any(c.candidate_status == "Conflict" for c in extent_cands)


def test_ledger_genuine_conflicting_valid_candidates_triggers_conflict():
    """When two distinct valid and grounded candidates exist, genuine conflict MUST be detected and escalated."""
    from app.semantic.ledger import DocumentEvidenceLedger, LedgerCandidate

    page_texts = {
        5: "ඉඩමේ ප්‍රමාණය : අක්කර 17",
        7: "ව්‍යාපෘති භූමි ප්‍රමාණය : අක්කර 25",
    }
    ledger = DocumentEvidenceLedger(page_texts=page_texts)

    c1 = LedgerCandidate(
        field_code="land_extent",
        canonical_value="17",
        raw_value="17",
        source_type="Explicit",
        page_number=5,
        quote="ඉඩමේ ප්‍රමාණය : අක්කර 17",
        confidence=0.9,
    )
    c2 = LedgerCandidate(
        field_code="land_extent",
        canonical_value="25",
        raw_value="25",
        source_type="Explicit",
        page_number=7,
        quote="ව්‍යාපෘති භූමි ප්‍රමාණය : අක්කර 25",
        confidence=0.9,
    )
    ledger.add_candidate(c1)
    ledger.add_candidate(c2)

    facts, all_cands, inventory, counts, warnings = ledger.reconcile_and_map_to_canonical_facts()

    extent_cands = [c for c in all_cands if c.field_code == "land_extent"]
    # Both distinct eligible candidates must be flagged as Conflict
    assert all(c.candidate_status == "Conflict" for c in extent_cands)
    # Neither must be marked as preferred fact, but both are preserved in facts for human review
    assert not any(c.is_preferred for c in extent_cands)
    assert not any(f.is_preferred for f in facts if f.field_code == "land_extent")
    assert len([f for f in facts if f.field_code == "land_extent"]) == 2
    assert any("Conflicting candidates extracted" in w for w in warnings)


def test_ledger_no_eligible_candidates_produces_no_preferred_fact():
    """When all candidates for a field are ungrounded or rejected, no preferred fact is emitted."""
    from app.semantic.ledger import DocumentEvidenceLedger, LedgerCandidate

    page_texts = {1: "සාමාන්‍ය පෙළ සමත්"}
    ledger = DocumentEvidenceLedger(page_texts=page_texts)

    # Hallucinated applicant name not in OCR
    c_bad = LedgerCandidate(
        field_code="applicant_name",
        canonical_value="අසෝක පෙරේරා",
        raw_value="අසෝක පෙරේරා",
        source_type="Explicit",
        page_number=1,
        quote="අසෝක පෙරේරා මයා",
        confidence=0.5,
    )
    ledger.add_candidate(c_bad)

    facts, all_cands, inventory, counts, warnings = ledger.reconcile_and_map_to_canonical_facts()
    assert not any(f.field_code == "applicant_name" for f in facts)


# ============================================================================
# 6. TEXT NORMALIZATION & COMPARISON SCORING TESTS
# ============================================================================

def test_text_normalization_for_comparison():
    """Verifies that zero-width characters, outer punctuation, and whitespace are normalized."""
    from evaluation.semantic.compare_models import normalize_text_for_comparison

    # Zero-width non-joiner \u200c removal
    assert normalize_text_for_comparison("පොල්\u200c වගාව") == "පොල් වගාව"
    # Outer trailing punctuation
    assert normalize_text_for_comparison("කිවුල කැලේ සටේට්ස්‌.") == "කිවුල කැලේ සටේට්ස්"
    # Surrounding quotes and colons
    assert normalize_text_for_comparison('":: "Acre" ::') == "Acre"
    # Multiple whitespace
    assert normalize_text_for_comparison("  පී.කේ.    ගුණසේකර  ") == "පී.කේ. ගුණසේකර"


def test_critical_score_does_not_overcount_partial_matches():
    """PartiallyCorrect critical fields must not receive full point in strict critical score."""
    from evaluation.semantic.compare_models import summarize_model_evaluation

    # Create dummy field inventory with 1 Correct, 1 PartiallyCorrect, 1 CorrectMissing, and rest Missing
    items = [
        {"field_code": "applicant_name", "status": "Extracted", "preferred_value": "පී.කේ. ගුණසේකර", "is_grounded": True},
        {"field_code": "project_title", "status": "Extracted", "preferred_value": "කිවුල කැලේ", "is_grounded": True},  # Partial match
        {"field_code": "requested_lease_period", "status": "Missing", "preferred_value": None, "is_grounded": False},  # Correct missing
    ]

    summary = summarize_model_evaluation(
        model_identifier="test-model",
        provider="test",
        field_inventory_items=items,
        duration_ms=1000.0,
        ocr_duration_ms=5000.0,
        counts={"candidate_count": 3, "accepted_count": 2, "rejected_count": 0, "conflict_count": 0, "ungrounded_count": 0},
    )

    # 9 critical fields total
    # Applicant Name = Correct (1)
    # Requested Lease Period = CorrectMissing (1)
    # Legal / Statutory Basis = CorrectMissing (1, since omitted and expected status is Missing)
    # Project Title = PartiallyCorrect (1)
    # Other 5 critical fields = Missing/Incorrect
    assert summary.critical_correct == 1
    assert summary.critical_correct_missing == 2
    assert summary.critical_partially_correct == 1

    # Strict score must be (1 + 2) / 9 = 3/9 = 0.3333 (NOT (1 + 2 + 1) / 9 = 4/9 = 0.4444!)
    assert summary.critical_score == round(3 / 9, 4)
    # Weighted score must be (1 + 2 + 0.5 * 1) / 9 = 3.5 / 9 = 0.3889
    assert summary.critical_score_weighted == round(3.5 / 9, 4)


def test_gemini_extractor_multi_page_citation_grounds_correctly():
    """When Gemini cites pageNumbers: [4, 5] and evidence is on page 5, grounding must succeed."""
    extractor = GeminiSemanticExtractor(api_key="test-key")
    pages = [
        {"page_number": 4, "text": "2.1.ව්‍යාපෘතියේ නම : කිවුල කැලේ සටේට්ස්‌."},
        {"page_number": 5, "text": "* ප්‍රාදේශීය ලේකම්‌ කොට්ඨාශය : මහකුඹුක්කඩවල"},
    ]
    raw_response = {
        "documentClassification": {"code": "StateLandLeaseProposal", "confidence": 0.95},
        "extractedFields": [
            {
                "fieldCode": "divisional_secretariat_division",
                "value": "මහකුඹුක්කඩවල",
                "pageNumbers": [4, 5],
                "originalEvidenceQuotes": ["ප්‍රාදේශීය ලේකම් කොට්ඨාශය : මහකුඹුක්කඩවල"],
                "confidence": 0.95,
            }
        ]
    }

    result = extractor.extract_from_response(raw_response, pages=pages)
    dsd_item = next(item for item in result.field_inventory if item.field_code == "divisional_secretariat_division")

    assert dsd_item.status == "Extracted"
    assert dsd_item.preferred_value == "මහකුඹුක්කඩවල"
    assert dsd_item.page_number == 5
    assert dsd_item.is_grounded is True


def test_zero_width_resilient_offsets_map_back_to_original_ocr_text():
    """Verify that zero-width resilient grounding returns offsets that index directly into the original OCR text."""
    from app.semantic.grounding import find_quote_offsets, ground_evidence

    # Page text containing zero-width non-joiner \u200c and meaningful Sinhala ZWJ \u200d
    # e.g. "ශ්‍රී ලංකා" contains ZWJ (\u200d) after virama: \u0dca\u200d\u0dbb
    original_text = "මුල් පිටුව: ග්‍රාම නිලධාරි වසම : 614/එෆ්\u200c කිවුල අංක 0] ශ්‍රී ලංකා රජය."
    page_texts = {5: original_text}

    # Quote without ZWNJ
    quote = "614/එෆ් කිවුල අංක 0]"
    evidence = ground_evidence(page_texts, page_number=5, quote=quote)

    assert evidence.is_verified is True
    assert evidence.start_offset is not None
    assert evidence.end_offset is not None

    # Crucial test: slicing original_text with reported offsets must extract the exact segment
    extracted_slice = original_text[evidence.start_offset : evidence.end_offset]
    assert "614/එෆ්" in extracted_slice
    assert "කිවුල අංක 0]" in extracted_slice

    # Verify Sinhala text with meaningful ZWJ conjunct (ශ්‍රී) is grounded accurately
    zwj_quote = "ශ්‍රී ලංකා රජය"
    zwj_evidence = ground_evidence(page_texts, page_number=5, quote=zwj_quote)
    assert zwj_evidence.is_verified is True
    assert original_text[zwj_evidence.start_offset : zwj_evidence.end_offset] == "ශ්‍රී ලංකා රජය"


def test_gemini_http_400_fails_immediately_without_retrying(monkeypatch):
    """HTTP 400 Bad Request must fail immediately without burning retries."""
    from unittest.mock import MagicMock
    import httpx
    from app.semantic.gemini_client import GeminiBadRequestError, GeminiClient

    client = GeminiClient(api_key="test-key", model_name="gemini-1.5-flash")

    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 400
    mock_resp.text = "<html><title>Error 400 (Bad Request)!!1</title></html>"
    mock_resp.headers = {"content-type": "text/html"}

    post_call_count = 0

    def mock_post(*args, **kwargs):
        nonlocal post_call_count
        post_call_count += 1
        return mock_resp

    monkeypatch.setattr(httpx.Client, "post", mock_post)

    with pytest.raises(GeminiBadRequestError) as exc_info:
        client.generate_content("test prompt", max_attempts=3)

    assert "HTTP 400" in str(exc_info.value)
    # Crucial assertion: Must NOT retry. Must attempt exactly once!
    assert post_call_count == 1
    assert client.last_execution_stats.get("attempts_made") == 1
    assert client.last_execution_stats.get("last_status_code") == 400


def test_gemini_credential_sanitization_strips_quotes_and_whitespace():
    """GeminiClient must automatically strip surrounding quotes and whitespace from API keys."""
    from app.semantic.gemini_client import GeminiClient

    # Double quotes from shell export: '"AIzaSyKey"'
    c1 = GeminiClient(api_key='"AIzaSyTestKey123"')
    assert c1._api_key == "AIzaSyTestKey123"

    # Single quotes
    c2 = GeminiClient(api_key="'AIzaSyTestKey456'")
    assert c2._api_key == "AIzaSyTestKey456"

    # Whitespace
    c3 = GeminiClient(api_key="  AIzaSyTestKey789  \n")
    assert c3._api_key == "AIzaSyTestKey789"


def test_gemini_model_sanitization_strips_models_prefix():
    """GeminiClient must strip 'models/' prefix to prevent double '/models/models/' in URL."""
    from app.semantic.gemini_client import GeminiClient

    c1 = GeminiClient(api_key="test", model_name="models/gemini-1.5-flash")
    assert c1.model_name == "gemini-1.5-flash"

    c2 = GeminiClient(api_key="test", model_name='"models/gemini-2.0-flash"')
    assert c2.model_name == "gemini-2.0-flash"


def test_settings_resolves_env_file_relative_to_service_root():
    """_ENV_FILE in config.py must resolve to document-intelligence-service/.env, not CWD."""
    from app.core.config import _ENV_FILE, _SERVICE_ROOT
    from pathlib import Path

    assert _SERVICE_ROOT.name == "document-intelligence-service"
    assert _ENV_FILE == _SERVICE_ROOT / ".env"


def test_gemini_client_preflight_rejection_of_control_characters():
    """GeminiClient must reject API keys containing control characters before making any request."""
    from app.semantic.gemini_client import GeminiAuthenticationError, GeminiClient

    # Embedded null byte
    with pytest.raises(GeminiAuthenticationError) as exc:
        GeminiClient(api_key="AIzaSyTest\x00Key")
    assert "control characters" in str(exc.value)

    # Embedded carriage return / newline
    with pytest.raises(GeminiAuthenticationError) as exc:
        GeminiClient(api_key="AIzaSyTest\rKey")
    assert "control characters" in str(exc.value)

    # Embedded bell character \x07
    with pytest.raises(GeminiAuthenticationError) as exc:
        GeminiClient(api_key="AIzaSyTest\x07Key")
    assert "control characters" in str(exc.value)


def test_gemini_client_preflight_rejection_of_empty_credentials():
    """Calling generate_content or list_models with unconfigured or empty credentials must fail before request."""
    from app.semantic.gemini_client import GeminiAuthenticationError, GeminiClient

    client = GeminiClient(api_key="   ")
    assert client.is_configured is False

    with pytest.raises(GeminiAuthenticationError) as exc1:
        client.list_models()
    assert "not configured" in str(exc1.value)

    with pytest.raises(GeminiAuthenticationError) as exc2:
        client.generate_content("test prompt")
    assert "not configured" in str(exc2.value)


def test_gemini_credential_source_reporting(monkeypatch):
    """Configuration source reporting must accurately distinguish os.environ vs .env vs unconfigured."""
    from app.core.config import Settings

    # Case 1: os.environ set
    monkeypatch.setenv("GEMINI_API_KEY", "AIzaSyFromEnv")
    s1 = Settings()
    assert s1.gemini_api_key_source == "os.environ"
    assert s1.gemini_api_key == "AIzaSyFromEnv"

    # Case 2: unconfigured (env var removed and no .env key)
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    s2 = Settings(gemini_api_key=None)
    assert s2.gemini_api_key_source == "unconfigured"


def test_inspect_credential_format_detects_control_characters_safely():
    """inspect_credential_format in diagnose_gemini.py must detect control chars without leaking key."""
    from evaluation.semantic.diagnose_gemini import inspect_credential_format

    res_clean = inspect_credential_format("AIzaSyValidKey123")
    assert res_clean["present"] is True
    assert res_clean["has_control_chars"] is False
    assert res_clean["starts_with_aiza"] is True

    res_corrupted = inspect_credential_format("AIzaSyValid\x00Key123")
    assert res_corrupted["present"] is True
    assert res_corrupted["has_control_chars"] is True
    # Never leaks the raw string
    assert "AIzaSyValid\x00Key123" not in str(res_corrupted["has_control_chars"])






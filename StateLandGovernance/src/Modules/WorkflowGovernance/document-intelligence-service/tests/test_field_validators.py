"""
Comprehensive deterministic unit tests for all 17 WorkflowGovernance proposal extraction fields.
Tests valid acceptance, invalid rejection, missing/abstention, multi-candidate ranking, and cross-field consistency.
"""

import pytest
from app.semantic.field_validators import (
    CANONICAL_DISTRICTS,
    DISTRICT_ALIASES,
    execute_cross_field_validation,
    validate_applicant_name,
    validate_applicant_type,
    validate_candidate_field,
    validate_district,
    validate_divisional_secretariat_division,
    validate_expected_employment,
    validate_investment_amount,
    validate_investment_currency,
    validate_land_extent,
    validate_land_extent_unit,
    validate_land_location,
    validate_legal_statutory_basis,
    validate_project_description,
    validate_project_purpose,
    validate_project_title,
    validate_proposed_land_use,
    validate_requested_lease_period,
    validate_supporting_document_references,
)
from app.semantic.validator import resolve_and_inventory_candidates
from app.semantic.extractor import SemanticFactDto


# === 1. APPLICANT NAME TESTS ===
def test_applicant_name_valid_acceptance():
    res = validate_applicant_name("P.K. Gunasekara", "Applicant: P.K. Gunasekara", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "P.K. Gunasekara"


def test_applicant_name_officer_rejection():
    # Government development officer must not be accepted as applicant
    res = validate_applicant_name("Coconut Development Officer", "Coconut Development Officer Recommendation", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("designation" in r for r in res.reasons)


def test_applicant_name_page_prior_warning():
    res = validate_applicant_name("Sunil Perera", "Applicant: Sunil Perera", page_number=12)
    assert res.is_valid is True
    assert res.status == "NeedsReview"
    assert any("page prior" in r for r in res.reasons)


# === 2. APPLICANT TYPE TESTS ===
def test_applicant_type_explicit_acceptance():
    res = validate_applicant_type("Private Limited", "Company Type: Private Limited", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Private Company"


def test_applicant_type_derived_from_suffix():
    res = validate_applicant_type("ABC Holdings (Pvt) Ltd", "ABC Holdings (Pvt) Ltd", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Private Company"


def test_applicant_type_job_title_rejection():
    res = validate_applicant_type("General Manager", "Position: General Manager", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("job title" in r for r in res.reasons)


# === 3. PROJECT TITLE TESTS ===
def test_project_title_cover_page_acceptance():
    res = validate_project_title("Kiwula Kele Estates", "Project: Kiwula Kele Estates", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Kiwula Kele Estates"


def test_project_title_turnover_table_rejection():
    # Page 10 of Proposal1.pdf: Total turnover table heading must be rejected!
    res = validate_project_title("වා9ාපෘනියේ මුළු පිරිවැටුම", "වා9ාපෘනියේ මුළු පිරිවැටුම අපේක්ෂා කරනු ලබයි", page_number=10)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("financial turnover" in r for r in res.reasons)


def test_project_title_officer_designation_rejection():
    # Page 17 of Proposal1.pdf: Officer designation must be rejected!
    res = validate_project_title("පොල්‌ සංවර්ධන නිලධාරි", "පොල්‌ සංවර්ධන නිලධාරි නිර්දේශය", page_number=17)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("officer designation" in r for r in res.reasons)


# === 4. PROJECT PURPOSE TESTS ===
def test_project_purpose_valid_acceptance():
    # Valid English objective sentence
    res = validate_project_purpose("Establish commercial coconut plantation and dairy farm", "Objective: Establish commercial coconut plantation and dairy farm", page_number=2)
    assert res.is_valid is True
    assert res.status == "Accepted"

    # Valid Sinhala objective sentence
    res_sin = validate_project_purpose("පොල් වගා ව්‍යාපෘතියක් ආරම්භ කිරීම", "අරමුණ: පොල් වගා ව්‍යාපෘතියක් ආරම්භ කිරීම", page_number=2)
    assert res_sin.is_valid is True
    assert res_sin.status == "Accepted"


def test_project_purpose_district_only_rejection():
    # Pure district names must never be accepted as project purpose
    res = validate_project_purpose("Kurunegala", "Location: Kurunegala", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("district" in r or "isolated noun" in r for r in res.reasons)

    res_sin = validate_project_purpose("පුත්තලම", "පුත්තලම", page_number=1)
    assert res_sin.is_valid is False
    assert res_sin.status == "Rejected"


def test_project_purpose_dsd_only_rejection():
    # Pure DSD or administrative-division phrases must be rejected
    res = validate_project_purpose("Mahakumbukkadawala", "Mahakumbukkadawala", page_number=18)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("isolated noun" in r or "administrative" in r for r in res.reasons)

    res_sin = validate_project_purpose("ම්හකුඹුක්කඩවල", "ම්හකුඹුක්කඩවල", page_number=18)
    assert res_sin.is_valid is False
    assert res_sin.status == "Rejected"


def test_project_purpose_officer_designation_rejection():
    # Official designations must be rejected from project_purpose
    res = validate_project_purpose("Coconut Development Officer", "Endorsement: Coconut Development Officer", page_number=18)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("designation" in r or "staff title" in r for r in res.reasons)

    res_sin = validate_project_purpose("පොල් සංවර්ධන නිළධාරි", "පොල් සංවර්ධන නිළධාරි නිර්දේශය", page_number=18)
    assert res_sin.is_valid is False
    assert res_sin.status == "Rejected"


def test_project_purpose_financial_table_rejection():
    # Single numeric or financial/turnover lines must be rejected
    res = validate_project_purpose("LKR 15,000,000", "Cost: LKR 15,000,000", page_number=5)
    assert res.is_valid is False
    assert res.status == "Rejected"

    res_turnover = validate_project_purpose("වාර්ෂික පිරිවැටුම රු. 2,000,000", "වාර්ෂික පිරිවැටුම රු. 2,000,000", page_number=10)
    assert res_turnover.is_valid is False
    assert res_turnover.status == "Rejected"


# === 5. LAND LOCATION TESTS ===
def test_land_location_explicit_site_acceptance():
    # Explicit site / parcel description accepted
    res = validate_land_location("Lot 14, Commercial Zone, Biyagama DSD", "Land situated at Lot 14, Commercial Zone, Biyagama DSD", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_land_location_village_site_acceptance():
    # Village / locality site accepted
    res = validate_land_location("Kuda Kiwula, Kottukachchiya", "Land situated at Kuda Kiwula, Kottukachchiya", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_land_location_district_only_context_acceptance():
    # District-only location remains valid when site context exists (Section 6)
    res = validate_land_location("Puttalam", "Proposed land is located in Puttalam district", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"

    res_sin = validate_land_location("පුත්තලම", "යෝජිත ඉඩම පිහිටි පුත්තලම දිස්ත්‍රික්කය", page_number=3)
    assert res_sin.is_valid is True
    assert res_sin.status == "Accepted"


def test_land_location_officer_designation_rejection():
    # Officer designations must be rejected from land_location
    res = validate_land_location("Coconut Development Officer", "Coconut Development Officer Recommendation", page_number=18)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("designation" in r or "staff title" in r for r in res.reasons)

    res_sin = validate_land_location("පොල් සංවර්ධන නිළධාරි", "පොල් සංවර්ධන නිළධාරි නිර්දේශය", page_number=18)
    assert res_sin.is_valid is False
    assert res_sin.status == "Rejected"

    res_ds = validate_land_location("Divisional Secretary", "Divisional Secretary Office", page_number=2)
    assert res_ds.is_valid is False
    assert res_ds.status == "Rejected"


def test_land_location_personal_name_rejection():
    # Personal names must be rejected from land_location
    res = validate_land_location("P.K. Gunasekara", "Signatory: P.K. Gunasekara", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("personal name" in r for r in res.reasons)


def test_land_location_financial_table_rejection():
    # Financial cost lines must be rejected from land_location
    res = validate_land_location("168,000.00", "පොහොර වියදම: 168,000.00", page_number=7)
    assert res.is_valid is False
    assert res.status == "Rejected"


def test_land_location_applicant_residence_rejection():
    res = validate_land_location("No 45 Galle Road", "Applicant's residential address: No 45 Galle Road", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("residential address" in r for r in res.reasons)


# === 6. DISTRICT TESTS ===
def test_district_canonical_english_acceptance():
    res = validate_district("Puttalam", "Puttalam District", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Puttalam"


def test_district_sinhala_alias_normalization():
    res = validate_district("පුත්තලම", "පුත්තලම දිස්ත්‍රික්කය", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Puttalam"


def test_district_dsd_locality_rejection():
    # Mahakumbukkadawala is a DSD, NOT a district! Must be strictly rejected!
    res = validate_district("Mahakumbukkadawala", "Mahakumbukkadawala", page_number=18)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("Divisional Secretariat" in r or "not resolve" in r for r in res.reasons)

    res_sin = validate_district("මහකුඹුක්කඩවල", "ම්හකුඹුක්කඩවල", page_number=18)
    assert res_sin.is_valid is False
    assert res_sin.status == "Rejected"


# === 7. DIVISIONAL SECRETARIAT DIVISION TESTS ===
def test_dsd_valid_acceptance():
    res = validate_divisional_secretariat_division("Mahakumbukkadawala", "DS Division: Mahakumbukkadawala", page_number=4)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Mahakumbukkadawala"


def test_dsd_district_substitution_rejection():
    # District name cannot substitute for DSD
    res = validate_divisional_secretariat_division("Puttalam", "DS Division: Puttalam", page_number=4)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("District name" in r for r in res.reasons)


# === 8. LAND EXTENT TESTS ===
def test_land_extent_numeric_acceptance():
    res = validate_land_extent("17", "Extent: 17 acres", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "17"


def test_land_extent_composite_acceptance():
    res = validate_land_extent("2A 1R 10P", "Extent: 2A 1R 10P", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert "2A 1R 10P" in res.normalized_value


def test_land_extent_money_rejection():
    res = validate_land_extent("Rs. 500,000", "Amount: Rs. 500,000", page_number=5)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === 9. LAND EXTENT UNIT TESTS ===
def test_land_extent_unit_valid_acceptance():
    res = validate_land_extent_unit("Acre", "17 Acres", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Acre"


def test_land_extent_unit_sinhala_acceptance():
    res = validate_land_extent_unit("අක්කර", "අක්කර 17", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Acre"


def test_land_extent_unit_unrecognized_rejection():
    res = validate_land_extent_unit("meters", "500 meters", page_number=3)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === 10. REQUESTED LEASE PERIOD TESTS ===
def test_requested_lease_period_valid_acceptance():
    res = validate_requested_lease_period("30 years", "Lease period of 30 years", page_number=4)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "30 years"


def test_requested_lease_period_construction_rejection():
    res = validate_requested_lease_period("6 months", "Construction period: 6 months", page_number=8)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === 11. PROPOSED LAND USE TESTS ===
def test_proposed_land_use_valid_acceptance():
    res = validate_proposed_land_use("Coconut Cultivation and Dairy Farming", "Proposed use: Coconut Cultivation and Dairy Farming", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_proposed_land_use_numeric_rejection():
    res = validate_proposed_land_use("12345", "12345", page_number=3)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === 12. INVESTMENT AMOUNT TESTS ===
def test_investment_amount_valid_acceptance():
    res = validate_investment_amount("15,000,000", "Total project investment: 15,000,000 LKR", page_number=7)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_investment_amount_produce_price_rejection():
    # Unit produce selling price (e.g. Rs 50-60 per coconut) must be rejected!
    res = validate_investment_amount("50-60", "9 ඒකකයක් රු; 50-60 අතර මිලකට වෙළඳ පොළට නිකුත් කිරීමට", page_number=10)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("selling price" in r for r in res.reasons)


# === 13. INVESTMENT CURRENCY TESTS ===
def test_investment_currency_lkr_acceptance():
    res = validate_investment_currency("LKR", "Amount: LKR 10,000,000", page_number=7)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "LKR"


def test_investment_currency_sinhala_rs_acceptance():
    res = validate_investment_currency("රු.", "මුදල රු. 5,000,000", page_number=7)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "LKR"


def test_investment_currency_invalid_rejection():
    res = validate_investment_currency("tokens", "1000 tokens", page_number=7)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === 14. EXPECTED EMPLOYMENT TESTS ===
def test_expected_employment_valid_acceptance():
    res = validate_expected_employment("25", "Employment generation: 25 direct jobs", page_number=6)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_expected_employment_beneficiaries_rejection():
    res = validate_expected_employment("500", "500 farming families will benefit", page_number=6)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("beneficiaries" in r for r in res.reasons)


# === 15. PROJECT DESCRIPTION TESTS ===
def test_project_description_valid_acceptance():
    long_desc = "The proposed project involves establishing a sustainable 17-acre coconut estate with modern drip irrigation."
    res = validate_project_description(long_desc, long_desc, page_number=2)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_project_description_isolated_yield_rejection():
    # Isolated yield sentence (e.g. 800 coconuts per acre expected) must be rejected!
    yield_desc = "9 එක් චාරයක දී අක්කරයකට පොල් ගෙඩි 800 අපේක්ෂා කරනු ලබයි."
    res = validate_project_description(yield_desc, yield_desc, page_number=10)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("isolated crop yield" in r for r in res.reasons)


# === 16. SUPPORTING DOCUMENT REFERENCES TESTS ===
def test_supporting_doc_valid_acceptance():
    res = validate_supporting_document_references("Survey Plan No. 4589/2024", "Attached Survey Plan No. 4589/2024", page_number=5)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_supporting_doc_statute_title_rejection():
    res = validate_supporting_document_references("State Lands Ordinance", "State Lands Ordinance", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === 17. LEGAL / STATUTORY BASIS TESTS ===
def test_legal_statutory_basis_valid_acceptance():
    res = validate_legal_statutory_basis("State Lands Ordinance No. 8 of 1947", "Under State Lands Ordinance No. 8 of 1947", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_legal_statutory_basis_officer_designation_rejection():
    res = validate_legal_statutory_basis("Coconut Development Officer", "Officer: Coconut Development Officer", page_number=17)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === MULTI-CANDIDATE MERGE & CONFLICT RESOLUTION TESTS ===
def test_merge_valid_page1_title_beats_later_page_heading():
    cands = [
        SemanticFactDto(
            field_code="project_title",
            value_kind="Text",
            canonical_value="වා9ාපෘනියේ මුළු පිරිවැටුම",  # Cost table
            confidence=0.98,
            page_number=10,
            quote="වා9ාපෘනියේ මුළු පිරිවැටුම",
            is_evidence_verified=True,
            is_semantic_valid=False,
            candidate_status="Rejected",
        ),
        SemanticFactDto(
            field_code="project_title",
            value_kind="Text",
            canonical_value="කිවුල කැලේ ස්ටේට්ස්",  # Cover title
            confidence=0.95,
            page_number=1,
            quote="කිවුල කැලේ ස්ටේට්ස්",
            is_evidence_verified=True,
            is_semantic_valid=True,
            candidate_status="Accepted",
        ),
    ]

    pref, inv, counts, warnings = resolve_and_inventory_candidates(cands)
    assert len(pref) == 1
    assert pref[0].canonical_value == "කිවුල කැලේ ස්ටේට්ස්"
    assert pref[0].is_preferred is True
    assert counts["accepted_count"] == 1
    assert counts["rejected_count"] == 1


def test_merge_grounded_beats_ungrounded():
    cands = [
        SemanticFactDto(
            field_code="district",
            value_kind="Text",
            canonical_value="Puttalam",
            confidence=0.85,
            page_number=3,
            quote="Puttalam District",
            is_evidence_verified=True,
            is_semantic_valid=True,
            candidate_status="Accepted",
        ),
        SemanticFactDto(
            field_code="district",
            value_kind="Text",
            canonical_value="Colombo",
            confidence=0.99,  # High model confidence but ungrounded!
            page_number=1,
            quote="Colombo",
            is_evidence_verified=False,
            is_semantic_valid=True,
            candidate_status="Ungrounded",
        ),
    ]

    pref, inv, counts, warnings = resolve_and_inventory_candidates(cands)
    assert len(pref) == 1
    assert pref[0].canonical_value == "Puttalam"
    assert counts["accepted_count"] == 1
    assert counts["ungrounded_count"] == 1


def test_merge_conflicting_candidates_preserved_without_silent_loss():
    cands = [
        SemanticFactDto(
            field_code="land_location",
            value_kind="Text",
            canonical_value="Parcel North Side",
            confidence=0.90,
            page_number=3,
            quote="Parcel North Side",
            is_evidence_verified=True,
            is_semantic_valid=True,
            candidate_status="Accepted",
        ),
        SemanticFactDto(
            field_code="land_location",
            value_kind="Text",
            canonical_value="Parcel South Side",
            confidence=0.90,
            page_number=3,
            quote="Parcel South Side",
            is_evidence_verified=True,
            is_semantic_valid=True,
            candidate_status="Accepted",
        ),
    ]

    pref, inv, counts, warnings = resolve_and_inventory_candidates(cands)
    # Neither candidate dominates -> marked as Conflict, both preserved for human verification
    assert len(pref) == 2
    assert all(p.candidate_status == "Conflict" for p in pref)
    assert counts["conflict_count"] == 2
    assert any("Conflicting candidates" in w for w in warnings)


def test_cross_field_district_matching_dsd_triggers_warning():
    facts = [
        {"fieldCode": "district", "canonicalValue": "Colombo"},
        {"fieldCode": "divisional_secretariat_division", "canonicalValue": "Colombo"},
    ]
    warnings = execute_cross_field_validation(facts)
    assert len(warnings) > 0
    assert any("Cross-field consistency warning" in w for w in warnings)


# === GENERALIZATION AND CROSS-FIELD VALIDATION TESTS (SECTIONS 14-18) ===

# Section 15: APPLICANT TESTS
def test_explicit_applicant_company_accepted():
    res = validate_applicant_name("Lanka Eco-Tourism PLC", "Applicant Company: Lanka Eco-Tourism PLC", page_number=2)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Lanka Eco-Tourism PLC"


def test_explicit_applicant_individual_accepted():
    res = validate_applicant_name("Kamal Bandara", "Proponent / Applicant: Kamal Bandara", page_number=1)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Kamal Bandara"


def test_project_title_rejected_as_applicant():
    res = validate_applicant_name("Solar Power Generation Facility", "Project Proposal: Solar Power Generation Facility", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("project, report, or activity heading" in r for r in res.reasons)


def test_officer_name_rejected_as_applicant_without_applicant_context():
    res = validate_applicant_name("Divisional Land Officer", "Report submitted to Divisional Land Officer", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("official or staff designation" in r for r in res.reasons)


def test_same_string_as_project_title_rejected_by_cross_field():
    cands = [
        SemanticFactDto(
            field_code="project_title",
            value_kind="Text",
            canonical_value="Commercial Hydroponics Farm",
            confidence=0.95,
            page_number=1,
            quote="Commercial Hydroponics Farm",
            is_evidence_verified=True,
            is_semantic_valid=True,
            candidate_status="Accepted",
        ),
        SemanticFactDto(
            field_code="applicant_name",
            value_kind="Text",
            canonical_value="Commercial Hydroponics Farm",
            confidence=0.90,
            page_number=1,
            quote="Commercial Hydroponics Farm",
            is_evidence_verified=True,
            is_semantic_valid=True,
            candidate_status="Accepted",
        ),
    ]
    pref, inv, counts, warnings = resolve_and_inventory_candidates(cands)
    app_inv = next(item for item in inv if item["field_code"] == "applicant_name")
    assert app_inv["status"] == "Rejected"
    assert app_inv["preferred_value"] is None
    assert any("identical to project_title" in w for w in warnings)


# Section 16: DSD TESTS
def test_dsd_with_explicit_administrative_label_accepted():
    res = validate_divisional_secretariat_division("Walapane", "Divisional Secretariat Division: Walapane", page_number=3)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Walapane"


def test_dsd_personal_name_rejected():
    res = validate_divisional_secretariat_division("P.K. Gunasekara", "P.K. Gunasekara මයා", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("personal name" in r for r in res.reasons)

    res_sin = validate_divisional_secretariat_division("පී.කේ. ගුණසේකර", "පී.කේ. ගුණසේකර මයා", page_number=1)
    assert res_sin.is_valid is False
    assert res_sin.status == "Rejected"
    assert any("personal name" in r for r in res_sin.reasons)


def test_dsd_district_name_alone_does_not_become_dsd():
    res = validate_divisional_secretariat_division("Kandy", "DS Division: Kandy", page_number=2)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("District name" in r for r in res.reasons)


def test_dsd_gn_division_does_not_become_dsd():
    res = validate_divisional_secretariat_division("452B Maligatenna", "Grama Niladhari Division 452B Maligatenna", page_number=2)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("Grama Niladhari" in r for r in res.reasons)


# Section 17: LAND EXTENT TESTS
def test_land_extent_acres_accepted():
    res = validate_land_extent("5 acres", "Land extent: 5 acres", page_number=2)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_land_extent_hectares_accepted():
    res = validate_land_extent("2.4 hectares", "Requested parcel: 2.4 hectares", page_number=2)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_land_extent_composite_arp_accepted():
    res = validate_land_extent("2A 1R 10P", "Parcel Area: 2A 1R 10P", page_number=2)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_land_extent_bare_integer_rejected():
    res = validate_land_extent("1", "1", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("bare number" in r for r in res.reasons)


def test_land_extent_money_rejected():
    res = validate_land_extent("Rs. 1,000,000", "Estimated Cost: Rs. 1,000,000", page_number=5)
    assert res.is_valid is False
    assert res.status == "Rejected"


def test_land_extent_duration_rejected():
    res = validate_land_extent("30 years", "Lease Term: 30 years", page_number=3)
    assert res.is_valid is False
    assert res.status == "Rejected"


# Section 18: SUPPORTING DOCUMENT TESTS
def test_supporting_doc_survey_plan_accepted():
    res = validate_supporting_document_references("Survey Plan No. 1234", "Attached: Survey Plan No. 1234", page_number=4)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_supporting_doc_annexure_approval_accepted():
    res = validate_supporting_document_references("Annexure 2 – Environmental Approval", "Refer Annexure 2 – Environmental Approval", page_number=6)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_supporting_doc_random_ocr_punctuation_rejected():
    res = validate_supporting_document_references("පති0801(160] '/111) ;", "පති0801(160] '/111) ;", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"
    assert any("lacks recognized document attachment" in r for r in res.reasons)


def test_supporting_doc_telephone_number_rejected():
    res = validate_supporting_document_references("+94 11 2345678", "Tel: +94 11 2345678", page_number=1)
    assert res.is_valid is False
    assert res.status == "Rejected"


# Section 14: NEUTRAL/ARTIFICIAL PROPOSAL GENERALIZATION TESTS
def test_tourism_proposal_title_on_page_2():
    res = validate_project_title("Yala Eco Resort Development", "Project Title: Yala Eco Resort Development", page_number=2)
    assert res.is_valid is True
    assert res.status in ("Accepted", "NeedsReview")


def test_industrial_proposal_applicant_on_page_4():
    res = validate_applicant_name("Lanka Steel Fabricators Ltd", "Applicant: Lanka Steel Fabricators Ltd", page_number=4)
    assert res.is_valid is True
    assert res.status in ("Accepted", "NeedsReview")


def test_proposal_dsd_appearing_later_page():
    res = validate_divisional_secretariat_division("Hingurakgoda", "Divisional Secretariat: Hingurakgoda", page_number=8)
    assert res.is_valid is True
    assert res.status == "Accepted"
    assert res.normalized_value == "Hingurakgoda"


def test_proposal_with_multiple_person_names_does_not_misidentify_dsd():
    res = validate_divisional_secretariat_division("Dr. Nimal Wickramasinghe", "Witness: Dr. Nimal Wickramasinghe", page_number=5)
    assert res.is_valid is False
    assert res.status == "Rejected"


# === Section 21: QWEN-SEMANTIC-QUALITY-2C TESTS ===

def test_investment_amount_percentage_rejected():
    """5% and 12.5% must NEVER be accepted as investment_amount."""
    res1 = validate_investment_amount("5%", "Weighting: 5%", page_number=1)
    assert res1.is_valid is False
    assert res1.status == "Rejected"
    assert any("percentage" in r.lower() for r in res1.reasons)

    res2 = validate_investment_amount("12.5%", "Allocation: 12.5%", page_number=2)
    assert res2.is_valid is False
    assert res2.status == "Rejected"


def test_investment_amount_duration_and_employee_rejected():
    """Time durations and employee counts must not be accepted as investment."""
    res_dur = validate_investment_amount("30 years", "Lease tenure: 30 years", page_number=1)
    assert res_dur.is_valid is False
    assert res_dur.status == "Rejected"

    res_emp = validate_investment_amount("10 employees", "Staff: 10 employees", page_number=2)
    assert res_emp.is_valid is False
    assert res_emp.status == "Rejected"


def test_investment_amount_grounded_monetary_accepted():
    """Valid monetary figures with currency or monetary units must be accepted."""
    res1 = validate_investment_amount("LKR 5,000,000", "Estimated Investment: LKR 5,000,000", page_number=3)
    assert res1.is_valid is True
    assert res1.status == "Accepted"

    res2 = validate_investment_amount("USD 250,000", "Project Capital: USD 250,000", page_number=3)
    assert res2.is_valid is True
    assert res2.status == "Accepted"

    res3 = validate_investment_amount("Rs. 2.5 million", "Total investment: Rs. 2.5 million", page_number=4)
    assert res3.is_valid is True
    assert res3.status == "Accepted"


def test_investment_amount_bare_number_with_explicit_context():
    """Bare number with explicit project cost context is accepted."""
    res = validate_investment_amount("5,000,000", "Total project cost: 5,000,000", page_number=4)
    assert res.is_valid is True
    assert res.status == "Accepted"


def test_investment_amount_bare_number_without_context_needs_review():
    """Bare number without monetary context requires human review."""
    res = validate_investment_amount("5000000", "Table item: 5000000", page_number=4)
    assert res.is_valid is False
    assert res.status == "NeedsReview"


def test_investment_currency_context_binding():
    """Currency binding: currency on same page as investment amount is retained; unlinked currency is flagged."""
    c_inv = SemanticFactDto(
        field_code="investment_amount",
        value_kind="Monetary",
        canonical_value="5000000",
        confidence=0.9,
        page_number=3,
        quote="Total project cost: LKR 5000000",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )
    c_curr_valid = SemanticFactDto(
        field_code="investment_currency",
        value_kind="Currency",
        canonical_value="LKR",
        confidence=0.9,
        page_number=3,
        quote="LKR",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )
    c_curr_unlinked = SemanticFactDto(
        field_code="investment_currency",
        value_kind="Currency",
        canonical_value="USD",
        confidence=0.7,
        page_number=12,
        quote="USD",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )

    _, _, _, _ = resolve_and_inventory_candidates([c_inv, c_curr_valid, c_curr_unlinked])
    assert c_curr_valid.candidate_status == "Accepted"
    assert c_curr_unlinked.candidate_status == "NeedsReview"


def test_land_extent_and_unit_companion_pairing():
    """Bare extent '5.5' paired with companion grounded unit 'Hectare' on same page is Accepted."""
    c_extent = SemanticFactDto(
        field_code="land_extent",
        value_kind="Area",
        canonical_value="5.5",
        confidence=0.9,
        page_number=2,
        quote="Extent: 5.5",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="NeedsReview",
    )
    c_unit = SemanticFactDto(
        field_code="land_extent_unit",
        value_kind="Unit",
        canonical_value="Hectare",
        confidence=0.9,
        page_number=2,
        quote="Unit: Hectare",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )

    preferred, inventory, counts, _ = resolve_and_inventory_candidates([c_extent, c_unit])
    assert c_extent.candidate_status == "Accepted"
    assert any("companion land_extent_unit" in r for r in c_extent.validation_reasons)
    ext_inv = next(item for item in inventory if item["field_code"] == "land_extent")
    assert ext_inv["status"] == "Extracted"
    assert ext_inv["preferred_value"] == "5.5"


def test_land_extent_bare_without_companion_unit_rejected():
    """Bare extent '5.5' without companion unit is Rejected."""
    c_extent = SemanticFactDto(
        field_code="land_extent",
        value_kind="Area",
        canonical_value="5.5",
        confidence=0.9,
        page_number=2,
        quote="5.5",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="NeedsReview",
    )
    preferred, inventory, counts, _ = resolve_and_inventory_candidates([c_extent])
    assert c_extent.candidate_status == "Rejected"
    ext_inv = next(item for item in inventory if item["field_code"] == "land_extent")
    assert ext_inv["status"] == "Rejected"


def test_land_extent_rejected_or_ungrounded_unit_cannot_rescue_extent():
    """Ungrounded unit cannot rescue bare land extent."""
    c_extent = SemanticFactDto(
        field_code="land_extent",
        value_kind="Area",
        canonical_value="5.5",
        confidence=0.9,
        page_number=2,
        quote="5.5",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="NeedsReview",
    )
    c_unit_ungrounded = SemanticFactDto(
        field_code="land_extent_unit",
        value_kind="Unit",
        canonical_value="Hectare",
        confidence=0.9,
        page_number=2,
        quote="Hectare",
        is_evidence_verified=False,  # NOT grounded!
        is_semantic_valid=True,
        candidate_status="Ungrounded",
    )
    preferred, inventory, counts, _ = resolve_and_inventory_candidates([c_extent, c_unit_ungrounded])
    assert c_extent.candidate_status == "Rejected"


def test_supporting_document_matches_project_title_rejected():
    """Supporting document candidate identical to project title without attachment framing is Rejected."""
    c_title = SemanticFactDto(
        field_code="project_title",
        value_kind="Text",
        canonical_value="Proposal Report Mark Sheet",
        confidence=0.9,
        page_number=1,
        quote="Proposal Report Mark Sheet",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )
    c_supp = SemanticFactDto(
        field_code="supporting_document_references",
        value_kind="Text",
        canonical_value="Proposal Report Mark Sheet",
        confidence=0.8,
        page_number=1,
        quote="Proposal Report Mark Sheet",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )
    _, _, _, _ = resolve_and_inventory_candidates([c_title, c_supp])
    assert c_supp.candidate_status == "Rejected"
    assert any("matches project title" in r for r in c_supp.validation_reasons)


def test_supporting_document_explicit_annex_accepted_even_with_title_word():
    """Explicit annex with attachment evidence is Accepted."""
    c_title = SemanticFactDto(
        field_code="project_title",
        value_kind="Text",
        canonical_value="Commercial Farm Development",
        confidence=0.9,
        page_number=1,
        quote="Commercial Farm Development",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )
    c_supp = SemanticFactDto(
        field_code="supporting_document_references",
        value_kind="Text",
        canonical_value="Annex 1: Feasibility Study",
        confidence=0.85,
        page_number=4,
        quote="Attached as Annex 1: Feasibility Study for Commercial Farm Development",
        is_evidence_verified=True,
        is_semantic_valid=True,
        candidate_status="Accepted",
    )
    _, _, _, _ = resolve_and_inventory_candidates([c_title, c_supp])
    assert c_supp.candidate_status == "Accepted"



"""
Human Reference Ground Truth for Proposal1.pdf across the 17 governed fields.

Authoritative reference established by expert manual review of the genuine
18-page Sinhala state land lease proposal document (Proposal1.pdf).
Used for controlled semantic-model comparison (Qwen3 1.7B vs Gemini 3.8 Flash).
"""

from dataclasses import dataclass, field
from typing import Dict, List, Optional


@dataclass
class HumanReferenceField:
    """Authoritative human ground truth specification for a single governed field."""

    field_code: str
    display_name: str
    is_critical: bool
    expected_status: str  # "Extracted" (present) or "Missing" (absent)
    reference_value: Optional[str]
    acceptable_variants: List[str] = field(default_factory=list)
    normalized_english_reference: Optional[str] = None
    expected_page_numbers: List[int] = field(default_factory=list)
    semantic_boundary_rule: str = ""


# 17 Governed Fields Ground Truth for Proposal1.pdf
PROPOSAL1_HUMAN_REFERENCE: Dict[str, HumanReferenceField] = {
    # ------------------------------------------------------------------------
    # 9 CRITICAL FIELDS
    # ------------------------------------------------------------------------
    "applicant_name": HumanReferenceField(
        field_code="applicant_name",
        display_name="Applicant Name",
        is_critical=True,
        expected_status="Extracted",
        reference_value="පී.කේ. ගුණසේකර",
        acceptable_variants=[
            "පී.කේ. ගුණසේකර",
            "පී.කේ. ගුණසේකර මයා",
            "පි,කේ. ගුණසේකර මයා",
            "P.K. Gunasekara",
            "P. K. Gunasekara",
        ],
        normalized_english_reference="P.K. Gunasekara",
        expected_page_numbers=[1, 3],
        semantic_boundary_rule="Must be the individual applicant, NOT an officer (e.g. CDO, GN, DS) or witness.",
    ),
    "applicant_type": HumanReferenceField(
        field_code="applicant_type",
        display_name="Applicant Type",
        is_critical=False,
        expected_status="Extracted",
        reference_value="Individual",
        acceptable_variants=["Individual", "තනි පුද්ගල", "පුද්ගලික"],
        normalized_english_reference="Individual",
        expected_page_numbers=[5],
        semantic_boundary_rule="Explicitly stated on page 5 item 2.6: 'තනි පුද්ගල' (Individual).",
    ),
    "project_title": HumanReferenceField(
        field_code="project_title",
        display_name="Project Title",
        is_critical=True,
        expected_status="Extracted",
        reference_value="කිවුල කැලේ එස්ටේට්ස්",
        acceptable_variants=[
            "කිවුල කැලේ එස්ටේට්ස්",
            "කිවුල කැලේ ස්ටේට්ස්‌",
            "කිවුල කැලේ සටේට්ස්‌",
            "Kiwula Kale Estates",
            "Kiwula Kale Estate",
        ],
        normalized_english_reference="Kiwula Kale Estates",
        expected_page_numbers=[1, 4],
        semantic_boundary_rule="Specific formal project name on cover and page 4 item 2.1.",
    ),
    "project_purpose": HumanReferenceField(
        field_code="project_purpose",
        display_name="Project Purpose",
        is_critical=False,
        expected_status="Extracted",
        reference_value="පොල් වගා කෘෂිකාර්මික සංවර්ධනය",
        acceptable_variants=[
            "පොල් වගා",
            "කෘෂිකාර්මික කටයුතු",
            "පොල් වගාව",
            "Commercial coconut cultivation",
            "Agricultural development",
            "Coconut cultivation",
        ],
        normalized_english_reference="Commercial coconut cultivation and agricultural development",
        expected_page_numbers=[1, 4],
        semantic_boundary_rule="Stated objective of land request; NOT an administrative area name or officer designation.",
    ),
    "land_location": HumanReferenceField(
        field_code="land_location",
        display_name="Land Location",
        is_critical=True,
        expected_status="Extracted",
        reference_value="කිවුල අංක 01, මහකුඹුක්කඩවල",
        acceptable_variants=[
            "කිවුල අංක 01",
            "614/එෆ් කිවුල අංක 01",
            "614/එෆ් කිවුල අංක 0]",
            "614/එෆ්‌ කිවුල අංක 0]",
            "කුඩා කිවුල",
            "Kiwula No 01",
            "Mahakumbukkadawala",
        ],
        normalized_english_reference="614/F Kiwula No. 01, Mahakumbukkadawala",
        expected_page_numbers=[3, 5],
        semantic_boundary_rule=(
            "Physical location of the land; NOT an officer designation or applicant name. "
            "Item 2.4 specifies GN Division '614/එෆ් කිවුල අංක 01'. Acceptable variants include the "
            "Tesseract OCR glyph variant '614/එෆ්‌ කිවුල අංක 0]' (where digit '1' is recognized as ']')."
        ),
    ),
    "district": HumanReferenceField(
        field_code="district",
        display_name="District",
        is_critical=True,
        expected_status="Extracted",
        reference_value="Puttalam",
        acceptable_variants=["Puttalam", "පුත්තලම", "පුත්තලම දිස්ත්‍රික්කය"],
        normalized_english_reference="Puttalam",
        expected_page_numbers=[4],
        semantic_boundary_rule="Canonical Sri Lankan administrative district. Page 4 explicitly names Puttalam district ('පුත්තලම දිස්ත්‍රික්කය').",
    ),
    "divisional_secretariat_division": HumanReferenceField(
        field_code="divisional_secretariat_division",
        display_name="Divisional Secretariat Division",
        is_critical=True,
        expected_status="Extracted",
        reference_value="මහකුඹුක්කඩවල",
        acceptable_variants=[
            "මහකුඹුක්කඩවල",
            "මකහකුඹුක්කඩවල",
            "Mahakumbukkadawala",
        ],
        normalized_english_reference="Mahakumbukkadawala",
        expected_page_numbers=[3, 5],
        semantic_boundary_rule="Official DSD under which the land falls (item 1.4 and item 2.4).",
    ),
    "land_extent": HumanReferenceField(
        field_code="land_extent",
        display_name="Land Extent",
        is_critical=True,
        expected_status="Extracted",
        reference_value="17",
        acceptable_variants=["17", "17.0", "අක්කර 17"],
        normalized_english_reference="17",
        expected_page_numbers=[4, 5, 6],
        semantic_boundary_rule="Numerical land extent. Explicitly stated as 17 acres across multiple pages.",
    ),
    "land_extent_unit": HumanReferenceField(
        field_code="land_extent_unit",
        display_name="Land Extent Unit",
        is_critical=False,
        expected_status="Extracted",
        reference_value="Acre",
        acceptable_variants=["Acre", "අක්කර", "acres"],
        normalized_english_reference="Acre",
        expected_page_numbers=[4, 5, 6],
        semantic_boundary_rule="Unit of measurement. Explicitly 'අක්කර' (Acre).",
    ),

    # ------------------------------------------------------------------------
    # REMAINING 8 GOVERNED FIELDS
    # ------------------------------------------------------------------------
    "requested_lease_period": HumanReferenceField(
        field_code="requested_lease_period",
        display_name="Requested Lease Period",
        is_critical=True,
        expected_status="Missing",
        reference_value=None,
        acceptable_variants=[],
        normalized_english_reference=None,
        expected_page_numbers=[],
        semantic_boundary_rule="CRITICAL: Proposal1 contains a 15-year building projection (page 5 item 2.7.II) and financial horizon, but NO explicit requested lease tenure. Must be Missing.",
    ),
    "proposed_land_use": HumanReferenceField(
        field_code="proposed_land_use",
        display_name="Proposed Land Use",
        is_critical=True,
        expected_status="Extracted",
        reference_value="කෘෂිකාර්මික කටයුතු / පොල් වගාව",
        acceptable_variants=[
            "කෘෂිකාර්මික කටයුතු",
            "පොල් වගාව",
            "කෘෂිකාර්මික",
            "Agriculture / Coconut Cultivation",
            "Coconut cultivation",
        ],
        normalized_english_reference="Agriculture / Coconut Cultivation",
        expected_page_numbers=[4],
        semantic_boundary_rule="Item 2.2 states 'කෘෂිකාර්මික කටයුතු' (Agricultural activities) and coconut cultivation.",
    ),
    "investment_amount": HumanReferenceField(
        field_code="investment_amount",
        display_name="Investment Amount",
        is_critical=False,
        expected_status="Extracted",
        reference_value="5,050,000",
        acceptable_variants=[
            "5,050,000",
            "5050000",
            "5.05 Million",
            "50.5 Lakhs",
        ],
        normalized_english_reference="5,050,000 LKR",
        expected_page_numbers=[11],
        semantic_boundary_rule=(
            "Item 09 total project capital cost/investment. Authentic visual inspection of Page 11 printed table "
            "confirms: Row 01 Lands (3,000,000) + Row 02 Buildings (500,000) + Row 03 Machinery (1,500,000) + "
            "Row 05 Other/වෙනත් (50,000) = Total/එකතුව '5,050,000.00'. "
            "The candidate value '5,000,000' extracted from OCR-truncated text is grounded but semantically Incorrect "
            "against the true physical ground truth of 5,050,000."
        ),
    ),
    "investment_currency": HumanReferenceField(
        field_code="investment_currency",
        display_name="Investment Currency",
        is_critical=False,
        expected_status="Extracted",
        reference_value="LKR",
        acceptable_variants=["LKR", "රු.", "රු", "Rs.", "Rs"],
        normalized_english_reference="LKR",
        expected_page_numbers=[11],
        semantic_boundary_rule="National currency stated as 'රු.' (Rupees / LKR).",
    ),
    "expected_employment": HumanReferenceField(
        field_code="expected_employment",
        display_name="Expected Employment",
        is_critical=False,
        expected_status="Extracted",
        reference_value="6",
        acceptable_variants=[
            "6",
            "6 (Planned)",
            "6 (Seasonal)",
            "6 (කාලීනව වෙනස් වේ)",
            "මුරකරු 01, කම්කරු 04, රියදුරු 01",
            "1 watcher, 4 laborers, 1 driver",
        ],
        normalized_english_reference="6 (Planned headcount; seasonally variable)",
        expected_page_numbers=[6, 7],
        semantic_boundary_rule=(
            "Derived planned headcount from Section 2.9 (Table 2.9 / doc page 6 / PDF page 7): "
            "Watcher: 01 male, Laborers: 03 female + 01 male = 04, Driver: 01 male; sum = 6. "
            "Document contains explicit footnote: 'ඉඩමෙහි සිදුවන වගා කටයුතු සඳහා කාලීනව කම්කරුවන්ගේ සංඛ්‍යාව වෙනස් වේ' "
            "(laborers vary seasonally). Represented as derived planned headcount of 6 with seasonal variation qualification. "
            "Historical reference provenance: previously classified as ExpectedMissing due to seasonal qualification note; "
            "updated in CLOSE-4B to Extracted (Derived) to reflect authentic table data."
        ),
    ),
    "project_description": HumanReferenceField(
        field_code="project_description",
        display_name="Project Description",
        is_critical=False,
        expected_status="Extracted",
        reference_value="අක්කර 17 ක භූමිය තුළ පොල් වගාව සිදුකිරීමේ ව්‍යාපෘතිය",
        acceptable_variants=[
            "අක්කර 17 ක භූමිය තුළ පොල් වගාව",
            "අක්කර 17 ක භ්‍රමිය තූල පොල්‌ වගාව",
            "අක්කර 17 ක භූමිය තුල පොල්‌ වගාව ,පොල්‌ සංවර්ධන නිලධාරි අධීක්ෂණය යටතේ වගා කිරීමට අපේක්ෂා කරනු ලබයි.",
            "අක්කර 17 ක භ්‍රමිය තූල පොල්‌ වගාව ,පොල්‌ සංචර්ධන නිලධාරි අධීක්ෂණය යටෙ වගා කිරීමට අපේක්ෂා කරනු ලබයි.",
            "Development of 17 acres of coconut cultivation",
            "Commercial coconut plantation on 17 acres",
        ],
        normalized_english_reference="Commercial coconut cultivation development across 17 acres in Puttalam district under supervision of Coconut Development Board.",
        expected_page_numbers=[4],
        semantic_boundary_rule="Summary narrative on page 4 introductory section.",
    ),
    "supporting_document_references": HumanReferenceField(
        field_code="supporting_document_references",
        display_name="Supporting Document References",
        is_critical=False,
        expected_status="Missing",
        reference_value=None,
        acceptable_variants=[],
        normalized_english_reference=None,
        expected_page_numbers=[],
        semantic_boundary_rule="No formal annexure numbers or deed citations attached in the scanned document. Expected Missing.",
    ),
    "legal_statutory_basis": HumanReferenceField(
        field_code="legal_statutory_basis",
        display_name="Legal / Statutory Basis",
        is_critical=True,
        expected_status="Missing",
        reference_value=None,
        acceptable_variants=[],
        normalized_english_reference=None,
        expected_page_numbers=[],
        semantic_boundary_rule="No explicit statute or Act (e.g. State Lands Ordinance) cited in document text. Expected Missing.",
    ),
}

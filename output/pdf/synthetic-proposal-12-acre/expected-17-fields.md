# Expected extraction values — comparison guide only

These are expected candidates for a manual OCR/Gemini test, not actual extraction results, verified facts or runtime seeds. Wording may vary; preserve exact quantities and source pages. Do not paste this file into the database.

| Field | Expected value | Primary page |
| --- | --- | --- |
| applicant_name | Mira Senali Testdeniya | 1 |
| applicant_type | Individual | 1 |
| project_title | Lantern Field Papaya and Vegetable Project | 1 |
| project_purpose | Establish a small irrigated papaya and vegetable farm for the domestic market | 1 |
| land_location | DEVELOPMENT_MOCK_FARM_12A, SYNTHETIC TEST ACCESS ROAD R-TEST-01, DEVELOPMENT_MOCK_GN_001 | 2 |
| district | DEVELOPMENT_MOCK_DISTRICT | 2 |
| divisional_secretariat_division | DEVELOPMENT_MOCK_DS | 2 |
| land_extent | 12 | 2 |
| land_extent_unit | acres | 2 |
| requested_lease_period | 15 years | 2 |
| proposed_land_use | Agriculture | 1 |
| investment_amount | 12000000 | 8 |
| investment_currency | LKR | 8 |
| expected_employment | 6 full-time positions including owner-manager; 5 hired staff | 5 |
| project_description | 6 acres papaya, 4 acres seasonal vegetables, 1 acre infrastructure and 1 acre conservation; irrigated domestic-market farm | 1 |
| supporting_document_references | Internal annexes: site layout p3; development/access sketches p4; employment/input schedules p5; financial schedules pp7–9; implementation p10; environment/declaration p11. No external certificates attached. | 11 |
| legal_statutory_basis | User-project test template STD-PROP-2026-V1 version 1.0 only; no verified statutory citation or official approval claimed. | 11 |

C4 normalization: 12 acres = 4.85622770688 hectares; 15 years; investment 12000000 LKR. ProposalVersion must remain the uploaded document-version UUID, not this reference or a fingerprint. C4 source pages must come from the actual reviewed run. Province/GN, parcel/evidence coverage and effective policy versions come from the separately selected scenario, not additional mandatory extraction fields.

The legal/statutory-basis answer explicitly states that no verified law or approval is claimed. Do not turn that into an invented citation. If the extractor returns NoCandidate or Unsupported here, inspect the source and preserve that distinction; this field is not automatically a mandatory proposal section.

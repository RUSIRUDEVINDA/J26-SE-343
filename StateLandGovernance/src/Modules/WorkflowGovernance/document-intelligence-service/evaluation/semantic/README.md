# Controlled Semantic-Model Comparison: Qwen3 1.7B vs Gemini 3.8 Flash

## Overview
This evaluation suite executes an empirical, controlled A/B semantic extraction comparison between the local baseline model (**Ollama `qwen3:1.7b`**) and the cloud candidate model (**Google Gemini `gemini-3.8-flash`**) on official Sri Lankan state land lease proposals.

## Invariant Comparison Conditions
1. **Identical OCR Text**: Both models receive the exact same raw Tesseract OCR output page-by-page (`=== PAGE X ===\n<OCR text>`). Gemini does not consume Qwen intermediate ledger or summaries.
2. **Governed 17-Field Catalog**: Both models are evaluated strictly against the governed 17 canonical proposal fields.
3. **Common Grounding & Validation Engine**: Both models are subject to identical deterministic grounding (`ground_evidence`) and field validation (`validate_candidate_field`).
4. **Verbatim Sinhala Grounding**: English translations alone are never accepted as evidence; ungrounded quotes are marked `Ungrounded` and rejected from preferred facts.
5. **Architectural Isolation**:
   - `GovernanceIntelligence` (Component 4) is **READ-ONLY** (0 files modified).
   - `WorkflowGovernance.Domain` is **STRICTLY FROZEN** (0 files modified).
   - Neither model has legal decision authority, routing authority, or institutional approval rights.
   - On Gemini API errors, the system cleanly abstains without falling back automatically to Qwen during benchmarks.

## Security Protocol
- `GEMINI_API_KEY` is never hardcoded, printed, or committed.
- Keys are read strictly from environment variables or gitignored `.env`.
- API keys are transmitted via `x-goog-api-key` headers over TLS.

## Authoritative Human Reference Answers (`Proposal1.pdf`)
| # | Field Code | Critical | Expected Status | Consensus Human Reference |
|---|---|---|---|---|
| 1 | `applicant_name` | Yes | Extracted | `පී.කේ. ගුණසේකර` (Individual) |
| 2 | `applicant_type` | Yes | Extracted | `Individual` (තනි පුද්ගල) |
| 3 | `project_title` | Yes | Extracted | `කිවුල කැලේ එස්ටේට්ස්` |
| 4 | `project_purpose` | Yes | Extracted | `පොල් වගා කෘෂිකාර්මික සංවර්ධනය` |
| 5 | `land_location` | Yes | Extracted | `කිවුල අංක 01, මහකුඹුක්කඩවල` |
| 6 | `district` | Yes | Extracted | `Puttalam` (පුත්තලම) |
| 7 | `divisional_secretariat_division` | Yes | Extracted | `මහකුඹුක්කඩවල` |
| 8 | `land_extent` | Yes | Extracted | `17` |
| 9 | `land_extent_unit` | Yes | Extracted | `Acre` (අක්කර) |
| 10 | `requested_lease_period` | No | Missing | _Missing_ (15-yr business horizon != lease tenure) |
| 11 | `proposed_land_use` | No | Extracted | `කෘෂිකාර්මික කටයුතු / පොල් වගාව` |
| 12 | `investment_amount` | No | Extracted | `5,050,000` (රු. 5,050,000 capital cost) |
| 13 | `investment_currency` | No | Extracted | `LKR` (රු.) |
| 14 | `expected_employment` | No | Missing | _Missing_ (Seasonal/variable breakdown) |
| 15 | `project_description` | No | Extracted | `අක්කර 17 ක භූමිය තුළ පොල් වගාව` |
| 16 | `supporting_document_references` | No | Missing | _Missing_ (No formal citations) |
| 17 | `legal_statutory_basis` | No | Missing | _Missing_ (No Act/Ordinance cited) |

## Evaluation Metrics
- **Field Verdicts**:
  - `Correct`: Extracted value matches human reference and evidence is grounded.
  - `PartiallyCorrect`: Matches in core substance with minor normalization differences.
  - `Incorrect`: Extracted value contradicts reference or accepts an invalid fact.
  - `CorrectMissing`: Model abstained (`Missing`) when ground truth is Missing.
  - `FalsePositive`: Model extracted a value where ground truth is Missing.
- **17-Field Total Score**: `(Correct + CorrectMissing + 0.5 * PartiallyCorrect) / 17`
- **9-Critical Field Score**: `Critical Correct / 9`
- **Latency**: Measures OCR time, semantic model inference time, and total end-to-end duration.

## Execution
```bash
# Run comparison runner
python -m evaluation.semantic.compare_models

# Run unit and integration tests
python -m pytest tests/test_gemini_semantic.py
```


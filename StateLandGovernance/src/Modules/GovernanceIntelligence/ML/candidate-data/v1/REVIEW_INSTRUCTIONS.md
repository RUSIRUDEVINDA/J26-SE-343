## Synthetic Family Review Pack — Instructions

File: `synthetic_family_review_pack.csv`
Rows: 100 (one per synthetic family)
Generated: 2026-10-01

---

### What this file contains

Each row represents one synthetic family consisting of three text variants
(Variant 1, 2, and 3) that were generated to represent the same governance
scenario. All three variants in a family share the same `Proposed_Label`.

The texts are synthetic — they were constructed by an AI language model to
represent plausible Sri Lankan state-land lease governance complaint scenarios.
They are not derived from real complaint records or source documents.

`Scenario_Grounding` for all 300 synthetic records reads:
"Hypothetical; not derived from a specific baseline or source incident"

---

### Reviewer tasks (per row)

1. **Read all three variant texts** for the family.

2. **Check category suitability.** Does the proposed label match the
   governance issue described in the texts? Use the four-class taxonomy:
   - Administrative / Procedural / Integrity
   - Lease Revenue / Payment / Enforcement
   - Unauthorized Allocation / Transfer / Use
   - Protected / Environmental Lease Misuse

3. **Check variant consistency.** Do all three variants describe the same
   governance scenario, or do they drift into different issues?

4. **Flag problematic texts.** Use `Review_Decision` to record one of:
   - `Approved` — label is suitable, variants are consistent, text is in scope
   - `Label_Change` — governance issue is correct but label needs correction
     (record corrected label in `Approved_Label`)
   - `Rejected_Out_of_Scope` — scenario does not fit the four-class taxonomy
   - `Rejected_Ambiguous` — scenario is ambiguous or spans multiple categories
   - `Rejected_Multi_Issue` — text conflates more than one governance issue
   - `Rejected_Contradictory` — variants describe different governance issues
   - `Needs_Discussion` — uncertain; flag for team discussion

5. **Record any relationship to known baseline incidents** in
   `Related_Baseline_IDs`. If any variant text appears to describe a scenario
   that resembles a specific baseline record (by incident type, entity, or
   geography), record the relevant Research_IDs (e.g., `WEB-063;STATE-LEASE-001`).
   If no relationship is apparent, leave blank.
   Note: absence of a recorded relationship does not confirm independence.

6. **Variant_Consistency:** Record whether all 3 variants are consistent with
   each other:
   - `Consistent` — all three describe the same scenario
   - `Minor_variation` — slight phrasing differences but same scenario
   - `Inconsistent` — at least one variant describes a different scenario

7. **Record your name and date** in `Reviewer` and `Review_Date`.

8. **Add any notes** in `Review_Notes`.

---

### What reviewers must NOT do

- Do not approve a family because it is likely to improve model metrics.
- Do not reject a family because it is likely to decrease model metrics.
- Do not infer guilt, legal liability, or factual accuracy from the texts.
- Do not record your review as independent domain-expert validation —
  this is researcher review, not independent professional governance review.
- Do not modify the source dataset (`state_land_complaints_development_530.csv`).
- Do not alter `Proposed_Label` in this file — use `Approved_Label` instead.
- Do not pre-fill or auto-approve any field.

---

### Column reference

| Column | Meaning |
|---|---|
| Synthetic_Family_ID | Family identifier (e.g., SYN-FAM-001) |
| Research_ID_V1/V2/V3 | Research ID of each variant |
| Canonical_English_Text_V1/V2/V3 | Full text of each variant |
| Proposed_Label | AI-proposed label (same for all 3 variants) |
| Scenario_Grounding | Always "Hypothetical; not derived from a specific baseline or source incident" |
| Reviewer | Name of the reviewer completing this row |
| Review_Date | Date of review (YYYY-MM-DD) |
| Review_Decision | One of the values listed above |
| Approved_Label | Only fill if Review_Decision = Label_Change |
| Variant_Consistency | Consistent / Minor_variation / Inconsistent |
| Related_Baseline_IDs | Semicolon-separated baseline Research_IDs if related; blank otherwise |
| Review_Notes | Any additional observations |

---

### Usage in Experiment 4

Only families with `Review_Decision = Approved` (or `Label_Change` with a
filled `Approved_Label`) will be eligible for training augmentation.

Families with `Related_Baseline_IDs` recorded will be excluded from the
training augmentation for any fold where those baseline records are in the
evaluation split.

Reviewer identifications are researcher judgements, not legal determinations.

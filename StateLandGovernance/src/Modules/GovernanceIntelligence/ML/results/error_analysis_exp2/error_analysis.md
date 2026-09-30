# Experiment 2 Classification Error Analysis & Human-Review Report

## 1. Executive Summary & Baseline Context

This report provides an in-depth, read-only analysis of the **55 out-of-fold (OOF) classification errors** produced by **Experiment 2** (Source-and-Text Grouped Cross-Validation) for the State Land Lease Complaint Classifier in Component 4 (`GovernanceIntelligence`).

### Baseline Development Context
- **Dataset Size:** 200 records, exactly balanced across 4 target governance classes (50 records per class).
- **Grouping Strategy:** 143 combined groups (`Combined_Group`), formed by taking the union of known source groups (`Source_Group_ID`) and 5-word shingle Jaccard text similarity clusters ($\ge 0.50$).
- **Group-Size Distribution:**
  - 113 singletons (size 1)
  - 26 pairs (size 2)
  - 3 triplets (size 3)
  - 1 large cluster of 26 records (`CG-STATE-LEASE-030`)
- **Evaluation Performance:**
  - **Accuracy:** 0.7250 (145 correct, 55 errors)
  - **Macro-F1:** 0.7242
  - **Weighted-F1:** 0.7242
- **Nature of Results:** These are **development cross-validation results** (out-of-fold predictions), **not untouched test-set evaluations**. They represent model generalisation across separated sources and narrative templates.

---

## 2. Artifact & Record ID Confirmation

All Experiment 2 artifacts were joined by `Research_ID` and validated for strict one-to-one matching:
- `data/state_land_governance_confirmed_200.csv` (200 records)
- `results/experiment2_source_text/out_of_fold_predictions.csv` (200 records)
- `results/experiment2_source_text/grouping_manifest.csv` (200 records)
- `results/experiment2_source_text/misclassified_cases.csv` (55 records)

### Validation Result
- All 200 `Research_ID` keys match across files with zero duplicates.
- The 55 error IDs in `misclassified_cases.csv` **identically equal** the 55 rows in `out_of_fold_predictions.csv` where `True_Label != Predicted_Label` (symmetric difference is empty set $\emptyset$).

### Complete List of the 55 Confirmed Error IDs
1. `STATE-LEASE-003`
2. `STATE-LEASE-004`
3. `STATE-LEASE-006`
4. `STATE-LEASE-007`
5. `STATE-LEASE-009`
6. `STATE-LEASE-012`
7. `STATE-LEASE-013`
8. `STATE-LEASE-017`
9. `STATE-LEASE-020`
10. `STATE-LEASE-023`
11. `STATE-LEASE-026`
12. `STATE-LEASE-029`
13. `STATE-LEASE-030`
14. `STATE-LEASE-031`
15. `STATE-LEASE-032`
16. `STATE-LEASE-035`
17. `STATE-LEASE-036`
18. `STATE-LEASE-042`
19. `STATE-LEASE-043`
20. `STATE-LEASE-044`
21. `STATE-LEASE-046`
22. `STATE-LEASE-047`
23. `STATE-LEASE-048`
24. `STATE-LEASE-051`
25. `STATE-LEASE-053`
26. `STATE-LEASE-055`
27. `STATE-LEASE-059`
28. `STATE-LEASE-061`
29. `STATE-LEASE-062`
30. `WEB-063`
31. `WEB-066`
32. `WEB-068`
33. `WEB-069`
34. `WEB-071`
35. `WEB-073`
36. `WEB-074`
37. `WEB-080`
38. `WEB-098`
39. `WEB-100`
40. `WEB-131`
41. `WEB-138`
42. `WEB-147`
43. `WEB-148`
44. `WEB-150`
45. `WEB-151`
46. `WEB-155`
47. `WEB-157`
48. `WEB-158`
49. `WEB-159`
50. `WEB-161`
51. `WEB-177`
52. `WEB-178`
53. `WEB-181`
54. `WEB-182`
55. `WEB-198`

---

## 3. Examination of Main Category Boundaries

Two major category boundaries account for **35 of the 55 errors (63.6%)**:
1. `Unauthorized Allocation / Transfer / Use` $\longleftrightarrow$ `Protected / Environmental Lease Misuse` (20 errors)
2. `Administrative / Procedural / Integrity` $\longleftrightarrow$ `Lease Revenue / Payment / Enforcement` (15 errors)

### Boundary 1: Unauthorized Allocation vs Environmental Lease Misuse (20 Errors)

#### Breakdown:
- **True: Unauthorized Allocation $\rightarrow$ Predicted: Protected / Environmental:** 13 records
  (`STATE-LEASE-009`, `STATE-LEASE-026`, `STATE-LEASE-062`, `WEB-131`, `WEB-138`, `WEB-147`, `WEB-148`, `WEB-150`, `WEB-151`, `WEB-155`, `WEB-157`, `WEB-158`, `WEB-161`)
- **True: Protected / Environmental $\rightarrow$ Predicted: Unauthorized Allocation:** 7 records
  (`STATE-LEASE-020`, `STATE-LEASE-023`, `WEB-177`, `WEB-178`, `WEB-181`, `WEB-182`, `WEB-198`)

#### Underlying Tension & Information Distinction:
- **Core Distinction in Project Taxonomy:**
  - *Unauthorized Allocation / Transfer / Use:* Governs **tenure and transactional legitimacy** — who holds the land, whether subleasing/transfer was approved, whether the permitted purpose was changed without authorization, or whether boundaries were crossed onto general state land.
  - *Protected / Environmental Lease Misuse:* Governs **physical and ecological integrity** — damage to natural reserves, sensitive ecosystems (mangroves, wetlands, forests, coastal reservations), soil/sand/clay mining, quarrying, and polluting activity.
- **Why Errors Occur:**
  1. *Resource extraction without authorization:* Records like `WEB-177`, `WEB-178`, `WEB-181`, and `WEB-182` describe soil/sand/stone extraction or quarrying. Because these complaints repeatedly state `"without authorization under the lease"` or `"without the required permit"`, the TF-IDF feature extractor heavily weights the authorization tokens, driving predictions into `Unauthorized Allocation`.
  2. *Encroachment and physical construction:* Records like `WEB-147`, `WEB-148`, `WEB-150`, `WEB-151` (expanding occupation beyond surveyed boundary) and `WEB-157`, `WEB-158`, `WEB-161` (building permanent structures without approval) involve physical modification of land. Because environmental cases also feature words like `"boundary"`, `"land"`, `"encroachment"`, and `"development"`, the model confuses general boundary expansion with reservation encroachment.

#### Contextual Exemplars (Correctly Classified):
- *Unauthorized Allocation Context:*
  - `STATE-LEASE-001` (Nawala): *"Allegation that state land obtained on a 99-year lease for cultivation was subdivided and sold to a private property company."* [Purely commercial tenure transfer]
  - `STATE-LEASE-010` (Kilinochchi): *"Allegation that government land reserved for cultivation was secretly leased for commercial buildings."* [Clear change of use / commercial lease]
- *Protected / Environmental Context:*
  - `STATE-LEASE-014` (Mirissa): *"Allegation that state land in a coastal area was illegally granted to foreign parties on long-term leases."* [Explicit coastal reservation context]
  - `STATE-LEASE-015` (Yala): *"Allegation that land associated with a wildlife zone was secretly leased for tourist-resort development."* [Explicit wildlife park buffer context]

#### Proposed Review Guideline (For Domain Stakeholders):
> **PROPOSED TIE-BREAKING RULE (Subject to Domain Review):**
> 1. If an incident involves **physical extraction of natural resources (sand, soil, minerals, stone)** or **damage to protected flora, fauna, water bodies, or designated ecological reservations**, classify as **Protected / Environmental Lease Misuse**, even if the complaint notes a lack of leasehold authorization or permit.
> 2. If an incident involves **transactional breach (subletting, transfer, sale, unapproved commercial building)** or **boundary encroachment onto non-reserved state land**, classify as **Unauthorized Allocation / Transfer / Use**, unless the land is explicitly within a statutory environmental/archaeological protection zone.

---

### Boundary 2: Administrative / Procedural / Integrity vs Lease Revenue / Payment / Enforcement (15 Errors)

#### Breakdown:
- **True: Lease Revenue $\rightarrow$ Predicted: Administrative:** 8 records
  (`STATE-LEASE-030`, `STATE-LEASE-031`, `STATE-LEASE-032`, `STATE-LEASE-035`, `STATE-LEASE-036`, `STATE-LEASE-044`, `STATE-LEASE-055`, `WEB-066`)
- **True: Administrative $\rightarrow$ Predicted: Lease Revenue:** 7 records
  (`STATE-LEASE-042`, `STATE-LEASE-043`, `WEB-063`, `WEB-068`, `WEB-069`, `WEB-080`, `WEB-100`)

#### Underlying Tension & Information Distinction:
- **Core Distinction in Project Taxonomy:**
  - *Administrative / Procedural / Integrity:* Focuses on **institutional conduct, governance bypass, and procedural adherence** — lack of Board/Cabinet approvals, competitive bidding bypass, bribery, document tampering, and failure to execute statutory covenants.
  - *Lease Revenue / Payment / Enforcement:* Focuses on **financial recovery, valuation, and default management** — overdue rentals, failure to collect arrears, non-revision of rental assessments, and failure to enforce monetary penalties.
- **Why Errors Occur:**
  1. *Dual Presence of Unsigned Agreements and Unpaid Arrears:* In official Auditor General findings (e.g. `STATE-LEASE-030`, `STATE-LEASE-031`, `STATE-LEASE-032`), the state failed to execute formal agreements *and* failed to collect millions in lease rentals. The text contains both procedural failure tokens (`"lease agreements had not been signed"`, `"no formal agreement"`) and monetary figures (`"Rs. 32,475,109 in lease rent due"`).
  2. *Valuation Delays and Development Inaction:* In records like `WEB-063` (Transworks Square) and `WEB-069` (Waters Edge), procedural delays in signing agreements or executing development covenants are accompanied by large revenue amounts (e.g. `"earned Rs. 10,719,033 in annual rent"`, `"Rs. 1,290.04 million difference"`), triggering revenue predictions for administrative cases.

#### Contextual Exemplars (Correctly Classified):
- *Administrative Context:*
  - `STATE-LEASE-002` (Mayura Place): *"Allegation that government suffered a loss ... when the land was leased in breach of procurement requirements."* [Focus is procurement bypass]
  - `STATE-LEASE-027` (UDA Colombo): *"COPE reported that the 6-acre UDA land was leased for 99 years in 2019 without Board approval..."* [Focus is institutional approval authority]
- *Lease Revenue Context:*
  - `STATE-LEASE-008` (Kalutara): *"Allegation that officials concealed the failure of companies leasing government land to pay lease rentals for several years."* [Focus is ongoing rental default concealment]
  - `STATE-LEASE-034` (LRC 2024): *"Audit reported Rs. 5.59 million in lease rent receivable 13 years after a lease for a rocky land plot..."* [Focus is overdue receivables collection]

#### Proposed Review Guideline (For Domain Stakeholders):
> **PROPOSED TIE-BREAKING RULE (Subject to Domain Review):**
> 1. If the primary governance finding or relief sought is the **computation, collection, or recovery of overdue rent, arrears, penalties, or statutory revaluation fees**, classify as **Lease Revenue / Payment / Enforcement**, even if administrative inaction contributed to the default.
> 2. If the primary governance issue is **systemic institutional non-compliance (procurement bypass, missing Cabinet/Board approval, bribery, corruption, or document falsification)**, classify as **Administrative / Procedural / Integrity**, even if monetary figures or financial losses are quantified.

---

## 4. Distribution of Errors & Subgroup Analysis

### 4.1 Confusion Matrix Across All 55 Errors

| True Label | Predicted Label | Error Count | Primary Underlying Factors |
|:---|:---|:---:|:---|
| Unauthorized Allocation | Protected / Environmental | 13 | Boundary encroachment / unapproved permanent building |
| Lease Revenue | Administrative | 8 | Unsigned agreements accompanying arrears |
| Administrative | Lease Revenue | 7 | Audit revenue figures mentioned in procedural cases |
| Protected / Environmental | Unauthorized Allocation | 7 | Resource extraction phrased as "without permit/authorization" |
| Lease Revenue | Protected / Environmental | 6 | Boilerplate template in Fold 3 with rural/estate names |
| Unauthorized Allocation | Lease Revenue | 4 | Long unlawful occupation regularised with rent arrears |
| Administrative | Unauthorized Allocation | 4 | Allocation without required approvals |
| Administrative | Protected / Environmental | 3 | Historical precinct or tourism land context |
| Protected / Environmental | Administrative | 1 | "Fraudulent lease agreements" token in forest reserve case |
| Lease Revenue | Unauthorized Allocation | 1 | Low-rent lease without Cabinet approval |
| Unauthorized Allocation | Administrative | 1 | Premature construction before lease clearance |
| **Total** | | **55** | |

---

### 4.2 Error Distribution by Fold

| Fold | Total Records | Correct | Errors | Error Rate | Fold Accuracy | Notes |
|:---:|:---:|:---:|:---:|:---:|:---:|:---|
| Fold 1 | 25 | 16 | 9 | 36.00% | 64.00% | Small fold; tourism idle-lease cases |
| Fold 2 | 39 | 32 | 7 | 17.95% | 82.05% | Strong performance on standard narratives |
| **Fold 3** | **59** | **35** | **24** | **40.68%** | **59.32%** | **Contains the 26-record boilerplate group `CG-STATE-LEASE-030`** |
| Fold 4 | 41 | 32 | 9 | 21.95% | 78.05% | Contains quarry/soil extraction group (`CG-WEB-177/178`) |
| Fold 5 | 36 | 30 | 6 | 16.67% | 83.33% | High accuracy on confirmed complaints |
| **Total** | **200** | **145** | **55** | **27.50%** | **72.50%** | |

---

### 4.3 Detailed Analysis of the Largest Combined Group (`CG-STATE-LEASE-030`)

The largest combined group in Experiment 2 is `CG-STATE-LEASE-030`, comprising **26 records**, all allocated to **Fold 3**.

- **Group Size:** 26 / 200 records (13.0% of the entire dataset).
- **Errors in Group:** **15 errors out of 26 records (57.69% error rate)**.
- **True Label Composition in Group:**
  - `Lease Revenue / Payment / Enforcement`: 17 records
  - `Administrative / Procedural / Integrity`: 7 records
  - `Unauthorized Allocation / Transfer / Use`: 2 records
- **Predicted Labels in Group:**
  - `Administrative / Procedural / Integrity`: 12
  - `Protected / Environmental Lease Misuse`: 7
  - `Lease Revenue / Payment / Enforcement`: 5
  - `Unauthorized Allocation / Transfer / Use`: 2
- **The Boilerplate Problem:**
  Ten of the error records in this group share the repetitive administrative audit template:
  > *" [Location/Estate] was identified in official audit/report material as a government/state land lease case involving [issue]. The Land Commissioner has confirmed that this is a genuine, direct, distinct state-land lease incident. File-level details should be retained from the Commissioner's records when available."*
  Because this entire cluster was placed in Fold 3 test, the training folds had very limited exposure to this exact template wording. Furthermore, the boilerplate tokens dominate the TF-IDF representation, masking the short substantive phrase (e.g. `"lease arrears / revocation"`). Consequently, estate names such as *"Kirimuttiwatta, Batticaloa"* or *"Kalawilawatta, Kalutara"* led the model to predict `Protected / Environmental Lease Misuse` for 6 lease revenue records.

---

### 4.4 Descriptive Results by Existing `Gold_Label_Status`

The dataset records two distinct label-review statuses:

| Existing `Gold_Label_Status` | Total Records | Correct | Errors | Error Rate | Accuracy | Dominant Error Dynamics |
|:---|:---:|:---:|:---:|:---:|:---:|:---|
| **Commissioner-confirmed label (user-confirmed)** | 120 | 102 | 18 | **15.00%** | **85.00%** | 15/18 errors are boundary overlap between Unauthorized and Environmental. Zero errors on Lease Revenue. |
| **Proposed research label - final human/domain review required** | 80 | 43 | 37 | **46.25%** | **53.75%** | Heavy multi-issue audit reports; includes 15 errors in `CG-STATE-LEASE-030`. |

#### Methodological & Confounding Caution:
- **Preserve Recorded Status:** These groups are defined strictly by their recorded `Gold_Label_Status`. They must **not** be renamed "real versus synthetic" or used to infer data provenance.
- **Confounding by Category Composition:**
  - The `Commissioner-confirmed` subset has 39 Environmental and 36 Unauthorized records, but only 20 Lease Revenue records.
  - The `Proposed research label` subset contains 30 Lease Revenue records (60% of all Lease Revenue cases in the dataset), of which 26 are concentrated in the single audit cluster `CG-STATE-LEASE-030`.
- **Confounding by Narrative Structure:**
  - `Commissioner-confirmed` narratives are standardized citizen/stakeholder complaint summaries (averaging 25–40 words), focusing on a single salient allegation.
  - `Proposed research label` narratives are either complex multi-clause Auditor General extracts (describing years of audit history, procedural lapses, and arrears simultaneously) or repetitive template entries.
  - The observed performance difference is therefore heavily confounded by text length, syntactic complexity, boilerplate density, and class distribution, rather than label quality alone.

---

### 4.5 Uncalibrated Model Probabilities & Uncertainty

In `out_of_fold_predictions.csv`, saved model probabilities represent **uncalibrated softmax outputs** from the logistic regression classifier.
- In 34 of the 55 error cases, the highest probability was below **0.42** (random chance across 4 balanced classes is 0.25).
- For example:
  - `STATE-LEASE-006`: Admin = 0.2582, Revenue = 0.1581, Environmental = 0.2827, Unauthorized = **0.3010** (True: Admin).
  - `STATE-LEASE-007`: Admin = 0.2401, Revenue = 0.1766, Environmental = **0.3076**, Unauthorized = 0.2757 (True: Admin).
- This near-uniform probability distribution reflects high informational entropy: the TF-IDF representation detects competing keyword signals from multiple classes in the same narrative, indicating genuine multi-issue overlap rather than severe misdirection.

---

## 5. Candidate Error Type Breakdown

In the review worksheet (`error_review.csv`), every error was assigned one provisional analyst category:

| Candidate Error Type | Count | % of Errors | Description |
|:---|:---:|:---:|:---|
| **Multiple issues present / primary-label ambiguity** | 40 | 72.7% | Narrative explicitly describes two or more distinct governance violations spanning different categories. |
| **Shared generic or boilerplate wording** | 8 | 14.5% | Repetitive audit metadata phrasing dilutes salient tokens, leading to misclassification. |
| **Clear category evidence but incorrect prediction** | 7 | 12.7% | Unambiguous category evidence exists in text, but model weights favored an incorrect class. |
| **Insufficient information in input text** | 0 | 0.0% | No records lacked sufficient basic text, though several lacked domain tie-breaking detail. |
| **Possible label inconsistency requiring review** | 0 | 0.0% | Flagged under multi-issue ambiguity for domain expert review. |
| **Undetermined** | 0 | 0.0% | All errors exhibited identifiable textual patterns. |
| **Total** | **55** | **100.0%** | |

### Human Domain Review Need:
- **Needs_Domain_Review = "Yes":** 48 records (87.3%)
- **Needs_Domain_Review = "No":** 7 records (12.7% — clear model mistakes on unambiguous text: `STATE-LEASE-009`, `STATE-LEASE-035`, `STATE-LEASE-036`, `WEB-071`, `WEB-131`, `WEB-138`, `WEB-155`).

---

## 6. Key Questions for Human / Domain Review

The following policy and classification questions must be resolved by domain experts (such as the Land Commissioner's Department or legal specialists):

1. **Resource Extraction vs Lease Conditions:** When a lessee extracts minerals, sand, or timber without authorization, is the primary governance risk treated as **Environmental Misuse** or **Unauthorized Use**?
2. **Boundary Encroachment Distinction:** Should encroachment beyond a surveyed boundary onto ordinary state land be distinguished from encroachment into environmentally protected buffer zones or statutory reserves?
3. **Compound Audit Findings:** In Auditor General reports where lease agreements were never signed AND rent was never paid, should **Revenue Recovery** take precedence over **Administrative Procedural Default**?
4. **Boilerplate Standardization:** Should the 26 records in `CG-STATE-LEASE-030` be re-annotated to remove audit boilerplate and retain only the specific incident facts before model training?
5. **Multi-Label Viability:** Given that 72.7% of errors exhibit multiple genuine governance issues, should Component 4 transition in a future phase from single-label multiclass to multi-label classification?

---

## 7. Generated Review Outputs

The analysis and review worksheet are saved under the ML results directory:

1. **Human-Review Worksheet (CSV):**
   `StateLandGovernance/src/Modules/GovernanceIntelligence/ML/results/error_analysis_exp2/error_review.csv`
   - Contains all 55 error records with 14 columns: `Research_ID`, `Canonical_English_Text`, `True_Label`, `Predicted_Label`, `Fold`, `Combined_Group`, `Gold_Label_Status`, `Narrative_Type`, `Candidate_Error_Type`, `Text_Evidence`, `Analysis_Note`, `Needs_Domain_Review`, `Reviewer_Decision` (blank), and `Reviewer_Notes` (blank).
   - Every `Text_Evidence` entry is an exact verbatim substring from the input text.

2. **Detailed Analytical Report (Markdown):**
   `StateLandGovernance/src/Modules/GovernanceIntelligence/ML/results/error_analysis_exp2/error_analysis.md`

---

## 8. Recommended Next Experiment

### Justification & Motivating Evidence
- **Observed Problem:** 40 of the 55 errors stem from multi-issue texts where peripheral administrative or procedural phrasing overwhelms the salient core issue, and 8 errors stem directly from repetitive audit metadata boilerplate in `CG-STATE-LEASE-030` (`"...was identified in official audit/report material as a government/state land lease case involving..."`).
- **Core Insight:** Tuning hyperparameters (e.g. C-regularization, n-gram range) cannot resolve cases where the input text contains misleading boilerplate or competing multi-class tokens. The most effective next technical intervention is **text preprocessing and salient-content normalization**.

### Proposed Experiment: Preprocessing & Boilerplate Removal Experiment (Experiment 3)
1. **Exact Proposed Change:**
   - Implement an automated text cleaner that strips standardized audit metadata headers and footers (e.g. `"was identified in official audit/report material as a government/state land lease case involving"`, `"The Land Commissioner has confirmed that this is a genuine, direct, distinct state-land lease incident"`, `"File-level details should be retained..."`).
   - Evaluate the unchanged TF-IDF + Logistic Regression model on the cleaned salient texts.
2. **What Will Remain Strictly Fixed for Fair Comparison:**
   - Identical 200 records and ground truth labels.
   - Identical 143 `Combined_Group` cross-validation fold assignments (5-fold StratifiedGroupKFold with seed 42).
   - Identical model architecture (TF-IDF word n-grams [1,2], sublinear TF, Logistic Regression with `C=1.0`, `solver='lbfgs'`).
3. **Evaluation Metrics to Compare:**
   - Primary: Overall **Macro-F1** and **Accuracy**.
   - Per-Class: Precision, Recall, F1, and False Positive Rate (FPR = FP / [FP + TN]) across all four categories.
   - Specific Error Metric: Error count in `CG-STATE-LEASE-030` (currently 15/26).
4. **Boundary Condition & Academic Caution:**
   - If human domain review confirms that multi-issue narratives represent inherent category overlap, source enrichment or multi-label formulation must be pursued rather than endless parameter tuning.
   - Metric improvement is an empirical research question and is **not guaranteed**.

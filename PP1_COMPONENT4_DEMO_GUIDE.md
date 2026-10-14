# Component 4 (Governance Intelligence) — PP1 Viva Demonstration Guide

> **Final-evidence note (2026-10-12):** The five `PP1-FINAL-*` records use synthetic C1/C3 inputs, while Component 4 calculations, frozen ML inference, PostgreSQL persistence and the case-linked local Besu anchor are real. The C3 referral handoff is synthetic and did not terminate a real C3 workflow. Audit pages show persisted last-verification facts, not a fresh verification. The original combined-case audit remains valid but case-unlinked; the corrected operation is discoverable through the additive structured compliance `CaseId`. The local outbox is a single-instance demonstration mechanism. Workflow-anomaly scores are statistical unusualness measures, not probabilities or misconduct findings.

**Project:** AI-Driven Digital Lease Governance for State Land Management  
**Component:** Component 4 — AI-Driven Governance, Compliance and Trust Infrastructure  
**Authoritative Boundary:** PostgreSQL is the single source of truth for administrative and case records. Hyperledger Besu (QBFT) serves exclusively as an immutable, tamper-evident trust anchor (storing zero PII, zero confidential data, only cryptographic digests).  
**Viva Date:** October 20, 2026  

---

## 1. Architecture & Services Summary

| Layer / Service | Tech Stack | Port / Endpoint | Health / Status Route |
| :--- | :--- | :--- | :--- |
| **Frontend Web App** | Next.js 16.3.5 (Turbopack) | `http://localhost:3000` | `GET /component-04` |
| **Backend API** | ASP.NET Core (.NET 8 Clean Architecture) | `http://localhost:5264` | `GET /api/governance-intelligence/dashboard` |
| **Database** | PostgreSQL 17 (`stateland_governance`) | `localhost:5432` | Schema: `governance_intelligence` |
| **Python ML Service** | FastAPI + Uvicorn (scikit-learn) | `http://127.0.0.1:8104` | `GET /health` |
| **Blockchain Microservice** | Go 1.26 + go-ethereum | `http://127.0.0.1:8550` | `GET /health` |
| **Blockchain Network** | 4-node Hyperledger Besu (QBFT, ChainID 1337) | `http://127.0.0.1:8545` | JSON-RPC `eth_blockNumber` |

---

## 2. Startup Commands (Quick-Launch Sequence)

Run each service in a separate terminal:

### A. Start PostgreSQL 17
Ensure PostgreSQL service is running locally on port `5432` with database `stateland_governance`.

### B. Start Hyperledger Besu QBFT Network
```powershell
docker compose -f governance-blockchain-service/network/besu/docker-compose.yml up -d
```
*Verify with:* `py -c "import urllib.request, json; print(urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:8545', data=b'{\"jsonrpc\":\"2.0\",\"method\":\"eth_blockNumber\",\"params\":[],\"id\":1}', headers={'Content-Type':'application/json'})).read().decode())"`

### C. Start Go Blockchain Microservice
```powershell
cd governance-blockchain-service
.\bin\api.exe
# Or compile and run:
# go run cmd/api/main.go
```
*Verify with:* `http://127.0.0.1:8550/health` (returns `{"chainId":1337,"ledgerConnected":true,"network":"Hyperledger Besu (QBFT)","status":"Healthy"}`)

### D. Start Python ML FastAPI Inference Service
```powershell
cd StateLandGovernance\src\Modules\GovernanceIntelligence\ML
py -m uvicorn src.http_service:app --host 127.0.0.1 --port 8104
```
*Verify with:* `http://127.0.0.1:8104/health`

### E. Start ASP.NET Core API Backend
```powershell
cd StateLandGovernance\src\Api
dotnet run
```
*Listening on:* `http://localhost:5264` (and Swagger at `http://localhost:5264/swagger`)

### F. Start Next.js Frontend
```powershell
cd StateLandGovernance.Web
npm run dev
```
*Listening on:* `http://localhost:3000`

---

## 3. Recommended Synthetic Demonstration Case Fixtures

Use the validated synthetic fixtures from `Component4_Synthetic_Integration_Seeds/`:

1. **`SIM-C4-2026-001` (Clean Agricultural Lease):**
   - Ideal for: Conditional Verification & Explainability.
   - Outcome: `FullySatisfied` conditions, `Low` severity explanation ("No Elevated Governance Issues Detected").
2. **`SIM-C4-2026-002` (High-Value Commercial Lease with Conflict):**
   - Ideal for: Multi-Institutional Consensus & Conflict Detection.
   - Outcome: Consensus `Blocked` due to mandatory institutional rejection (`Simulated Divisional Secretariat`).
3. **`SIM-C4-2026-003` (Disputed Multi-Authority Overlap):**
   - Ideal for: Authority Level Inconsistency Detection.
   - Outcome: Critical conflict between Provincial Office restriction and Divisional Secretariat approval.
4. **`SIM-C4-2026-005` (Elevated Financial Risk Case):**
   - Ideal for: Governance Risk Assessment.
   - Outcome: Triggered risk indicators and calculated risk score.
5. **`COMPLAINT-PP1-DEMO` (Real ML Text Input):**
   - Input: *"The divisional secretariat officer accepted unauthorized lease application without issuing formal receipts and bypassed gazetted valuation."*
   - Outcome: Real prediction -> `Administrative / Procedural / Integrity` (probability: ~0.37).
6. **`WORKFLOW-PP1-DEMO` (Real Isolation Forest Anomaly Detection):**
   - Input: Trace of 4 completed workflow steps.
   - Outcome: Anomaly score: `0.688`, threshold: `0.574` -> advisory statistical observation.

---

## 4. Frontend Demonstration Walkthrough (Step-by-Step)

Open your browser to: **`http://localhost:3000/component-04`**

### Step 1: Governance Intelligence Dashboard
- URL: `http://localhost:3000/component-04`
- View: Live aggregate statistics (Cases in view, Review required, Insufficient information, Recorded clear status) backed by real PostgreSQL data.
- Switch between **Live workspace** and **Explore demo data** using the top banner. Notice Live mode reflects real backend state and never substitutes mock data.

### Step 2: Live Regulatory Compliance Evaluation
- URL: `http://localhost:3000/component-04/cases/compliance`
- Form Input:
  - Action Name: `EvaluateNpdProposalCompliance`
  - Lease Duration: `35` years
  - Proposed Use: `Commercial Agriculture`
  - Lease Amount: `500000` LKR
  - Zoning Area: `Agricultural`
- Click: **Run Compliance Check**
- Observe: Real C# engine evaluates rules deterministically, returns `Conditional` status with 24 findings and deterministic hash ID.

### Step 3: Conflict Detection Engine
- URL: `http://localhost:3000/component-04/cases/conflicts`
- Click: Submit evaluation with multi-institution decision snapshots (`Simulated Provincial Office` vs `Simulated Divisional Secretariat`).
- Observe: Real engine identifies `AuthorityLevelInconsistency` (Critical severity) regarding subject `SIM-PARCEL-003`.

### Step 4: Governance Risk Assessment
- URL: `http://localhost:3000/component-04/risk`
- Paste JSON payload from `Component4_Synthetic_Integration_Seeds/inputs/risk/SIM-C4-2026-005.json`.
- Click: **Evaluate Governance Risk**
- Observe: Evaluates risk indicators, returns calculated risk score and recommended review actions.

### Step 5: Multi-Institutional Consensus Engine
- URL: `http://localhost:3000/component-04/consensus`
- Paste JSON payload from `Component4_Synthetic_Integration_Seeds/inputs/consensus/SIM-C4-2026-002.json`.
- Click: **Evaluate Consensus**
- Observe: Quorum evaluation, mandatory veto detection, returns `Blocked` status with detailed explanation.

### Step 6: Conditional Governance Verification
- URL: `http://localhost:3000/component-04/conditions`
- Paste JSON payload from `Component4_Synthetic_Integration_Seeds/inputs/conditions/SIM-C4-2026-001.json`.
- Click: **Verify Conditions**
- Observe: Evaluation of conditions and milestone requirements, returns `FullySatisfied`.

### Step 7: Explainable Governance Engine
- URL: `http://localhost:3000/component-04/explain`
- Paste JSON payload from `Component4_Synthetic_Integration_Seeds/inputs/explain/SIM-C4-2026-001.json`.
- Click: **Generate Plain-Language Explanation**
- Observe: Generates explainable, neutral, ethical governance language without legal guilt claims.

---

## 5. Machine Learning (ML) Inference & Verified Evaluation Evidence

### A. Complaint Classification Model v2 (`governance_complaint_classifier_v2`)
- **Active Model Version:** `v2` (promoted from Experiment 6 Arm E; `v1` preserved as explicit rollback via `GOVERNANCE_MODEL_VERSION=v1`)
- **Pipeline Architecture:** `BoilerplateStripper -> FeatureUnion(word TF-IDF unigrams+bigrams, character TF-IDF char_wb 3-5 grams) -> LogisticRegression (L2, C=1.0, class_weight='balanced', max_iter=2000, random_state=42)`
- **Frozen Artifact Hashes (SHA-256):**
  - `models/v2/governance_classifier_v2.joblib`: `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae` (1,001,516 bytes)
  - `models/v2/model_metadata_v2.json`: `70a4aba1936b72f2556df6e4baba5eb12e74e8846c51b338267fb7eb947a2d5c` (12,601 bytes)
  - `models/v2/validation/model_validation_v2.json`: `0d36fd51c7f71f1550a3dee4f08a2a0f44ff4500eca354128472131fbcc8760f` (9,577 bytes)
- **Training Population (425 total rows):**
  - 200 real baseline records (`sample_weight = 1.0`)
  - 225 approved development synthetic records across 75 families (`sample_weight = 1/3` per variant, `1.0` per family)
  - Strictly excluded: 30 source-derived candidates, all prospective test families, and any `Rejected` / `Needs_Discussion` records.

#### 1. Grouped Source-Derived & Development Evaluation (Experiment 5 — 5-Fold Group-Aware Cross-Validation):
- **Real Baseline Subgroup ($n = 200$, grouped by `Source_Group_ID`):**
  - Accuracy: **0.8000** (160 / 200 correct)
  - Balanced Accuracy: **0.8000**
  - Macro Precision / Recall / F1: **0.8019 / 0.8000 / 0.7989**
  - Weighted F1: **0.7989**
- **Approved Development Synthetic Subgroup ($n = 225$ across 75 families):**
  - Record-Level Accuracy: **0.9644** (217 / 225 correct), Macro-F1: **0.9657**
  - Family-Level Majority Accuracy ($75$ families): **0.9867**, Family Majority Macro-F1: **0.9878**
- **Combined Development Population ($n = 425$):**
  - Accuracy: **0.8871** (377 / 425 correct), Macro-F1: **0.8863**
- *(Note: Preserved v1 Experiment 1 baseline on the 200 real records alone using word-only TF-IDF had Accuracy `0.7450` and Macro-F1 `0.7402`.)*

#### 2. Held-Out Prospective Synthetic Evaluation (Experiment 6 — Frozen Arm E Candidate):
- **Evaluation Type:** Held-out prospective synthetic evaluation ($n = 69$ records across 23 eligible reviewed families; 2 families / 6 records excluded prior to scoring; zero ID, family, or text overlap with training).
- **Record-Level Metrics ($n = 69$):**
  - Accuracy: **0.9420** (65 / 69 correct; 95% Wilson CI: `[0.8602, 0.9772]`)
  - Balanced Accuracy: **0.9345**
  - Macro Precision: **0.9506** | Macro Recall: **0.9345** | **Macro-F1: 0.9410**
  - Weighted F1: **0.9430**
- **Family-Level Metrics ($23$ families):**
  - Family Majority Accuracy: **1.0000** | Family Majority Macro-F1: **1.0000** (19/23 families had all 3 variants correct; 4/23 had 2/3 correct).
- **Per-Class Prospective Synthetic Results ($n = 69$):**
  | Category | Support | Precision | Recall | F1 |
  | :--- | :---: | :---: | :---: | :---: |
  | `Administrative / Procedural / Integrity` | 21 | 1.0000 | 0.9524 | 0.9756 |
  | `Lease Revenue / Payment / Enforcement` | 9 | 1.0000 | 0.8889 | 0.9412 |
  | `Unauthorized Allocation / Transfer / Use` | 18 | 0.8500 | 0.9444 | 0.8947 |
  | `Protected / Environmental Lease Misuse` | 21 | 0.9524 | 0.9524 | 0.9524 |
- **Methodological & Research Boundary:** Experiment 6 is a synthetic prospective evaluation and must not be claimed as real-world departmental accuracy. Probabilities are uncalibrated logistic-regression outputs for advisory human review.

---

### B. Workflow Anomaly Detector v1 (`workflow_anomaly_model_v1` — Frozen Isolation Forest)
- **Model & Contract:** Unsupervised `IsolationForest` (`n_estimators=200, random_state=42`) fitted on 240 generator-designated reference-normal synthetic traces (`X_train.csv`).
- **Frozen Threshold:** `0.5743688923115832` (95th percentile of validation reference-normal scores; strict policy `anomaly_score > threshold`; no threshold tuning performed).
- **Frozen Bundle Hashes (SHA-256):**
  - Bundle Manifest: `50ca8d9042e60f98051684e0901b6831bee91c101d85488b0e22c4c301d480a7`
  - Model (`model.joblib`): `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`
  - Addendum (`classification_metrics_addendum.json`): `ec9cff6511fc30a56bb7476430684fee3cd7a6dc8efbb9fefecffc493061b38e`
- **Held-Out Labeled Synthetic Test Metrics ($n = 108$: 80 reference-normal, 28 perturbed anomalies):**
  - Accuracy: **0.8611** (95% Wilson CI: `[0.7834, 0.9140]`; +12.04% over always-normal baseline `0.7407`)
  - Balanced Accuracy: **0.8018**
  - Macro Precision: **0.8258** | Macro Recall: **0.8018** | **Macro-F1: 0.8125**
  - Weighted F1: **0.8585**
  - Anomaly Class Precision: **0.7600** | Anomaly Recall: **0.6786** | Anomaly F1: **0.7170**
  - Specificity (Reference-Normal True Negative Rate): **0.9250** (false-positive flag rate `0.0750`)
  - Confusion Counts: `TP = 19, TN = 74, FP = 6, FN = 9`
  - Per-Perturbation Detection: `extended_gap` (10/10), `duplicated_sequence` (4/6), `increased_event_count` (5/10), `excessive_handoffs` (0/2; small sample caution), `repeated_correction_loop` (0 test examples).
- **Live Inference Examples:**
  - Flagged trace (`SIM-C4-2026-001`, 5 events): Score `0.6883` > `0.5744` $\to$ `flagged: true`
  - Unflagged trace (`SL-CASE-0004`, 18 events): Score `0.4772` <= `0.5744` $\to$ `flagged: false`

---

## 6. Blockchain Trust Layer (Automatic Outbox Anchoring & Verification Evidence)

### End-to-End Automatic Outbox Lifecycle:
1. **Genuine Component 4 Evaluation Execution:**
   - Submitted `POST /api/governance-intelligence/evaluate-compliance` with `ActionName = "PP1-AUTO-OUTBOX-MATCH-1791737605"`.
   - `PostgresGovernanceEvaluationStore` atomically persisted the compliance evaluation, `GovernanceAuditRecord` (`daa5ffdc-1f60-4b83-837d-b8e6d1043d29`), and `GovernanceAuditAnchorOutbox` entry (`Status = 0 / Pending`) in a single PostgreSQL transaction.
2. **Canonical Hash Computation (`AUDIT-V1` in C#):**
   - `GovernanceAuditHasher` aligned the UTC timestamp to PostgreSQL microsecond resolution (`2026-10-11T16:53:25.6865390Z`) and computed the canonical SHA-256 digest:
   - `RecordHash`: `32efde6857cda1b07f40b2de10ce029b637757aefff75c8bd84fbae10ad01b32`
3. **Automatic Outbox Dispatch (`GovernanceAuditAnchorOutboxProcessor`):**
   - The hosted background service polled `governance_audit_anchor_outbox`, dispatched `POST http://localhost:8550/api/v1/anchors` to the Go microservice, and anchored the hash on the 4-node Hyperledger Besu QBFT network (`ChainID: 1337`).
   - Smart Contract Address: `0x42699A7612A82f1d9C36148af9C77354759b210b`
   - Transaction Reference: `0x11c30f02e09bd1dcac20065e1916f4dc9b542c661268b352efafa8d586b8f2d2`
   - Block Number: `8026`
   - Anchored At UTC: `2026-10-11T16:53:33Z`
   - Outbox entry marked `Completed (Status = 2)` and receipt saved to `governance_audit_anchor_receipts`.
4. **Live Frontend & API Verification (`Match`):**
   - `GET /api/governance-intelligence/audits/daa5ffdc-1f60-4b83-837d-b8e6d1043d29/receipt` $\to$ `AnchorStatus: "Anchored"`, `BlockNumber: 8026`.
   - `POST /api/governance-intelligence/audits/daa5ffdc-1f60-4b83-837d-b8e6d1043d29/verify` (and via `/component-04/verification` in Microsoft Edge) $\to$ `VerificationStatus: "Match"`, `Explanation: "The current off-chain record digest matches the anchored blockchain ledger record."`
5. **Previously Anchored Viva Record Also Verified:**
   - Record ID `818f49f5-d954-4a4a-9305-a18133d1e2a2` (`Block 1891`, tx `0xa461c6f0a2188b2989d840d34e534623695897a59e6b75eca60ca3aa223e2c49`) remains verifiable on-chain.

---

## 7. Backup Demonstration Procedure (If Docker/Blockchain is Offline)

If Docker is not running or Besu is stopped:
1. The ASP.NET Core backend gracefully catches connection errors and sets `VerificationStatus: "Unavailable"` without blocking or failing primary governance evaluations.
2. The Outbox processor persists pending anchors in `governance_intelligence.governance_audit_anchor_outbox` with exponential backoff for automatic retry once connectivity is restored.
3. In `governance-blockchain-service`, setting `BLOCKCHAIN_MODE=mock` enables the explicit development mock fallback (`NewMockLedgerClient`) when Docker is unavailable.

---

## 8. Viva Verification Status Summary

| Capability / Feature | Viva Status | Verification Evidence |
| :--- | :---: | :--- |
| **1. Production Frontend Build (Offline-Safe Fonts)** | **PASS** | `npm run build` succeeded in 1.9s TypeScript / 484ms static gen without Google Fonts network dependency |
| **2. Real .NET Regulatory Compliance Engine** | **PASS** | Live Edge submission $\to$ `200 OK` (`Conditional`, hash `a9935af8...`), persisted in PostgreSQL + outbox |
| **3. Real .NET Governance Conflict Engine** | **PASS** | Live Edge submission (`SIM-C4-2026-003`) $\to$ `200 OK` (`ConflictsDetected`), persisted in PostgreSQL |
| **4. Real .NET Governance Risk Engine** | **PASS** | Live Edge submission (`SIM-C4-2026-005`) $\to$ `200 OK` (`Score: 15`, `Low`), persisted in PostgreSQL |
| **5. Real .NET Explainable Governance Engine** | **PASS** | Live Edge submission (`SIM-C4-2026-001`) $\to$ `200 OK` (`72fdf8649e2d4be8`), persisted in PostgreSQL |
| **6. Real .NET Institutional Consensus Engine** | **PASS** | Live Edge submission (`SIM-C4-2026-002`) $\to$ `200 OK` (`Blocked`, `CNS-EVAL-6650cc9d6988c5fd`), persisted |
| **7. Real .NET Conditional Verification Engine** | **PASS** | Live Edge submission (`SIM-C4-2026-001`) $\to$ `200 OK` (`FullySatisfied`, `COND-VERIF-294969f18ee014f4`) |
| **8. Real Python Complaint Classifier v2** | **PASS** | Live Edge submission $\to$ `200 OK` (`v2`, `Administrative / Procedural / Integrity`), persisted & visible on `/cases` |
| **9. Real Python Workflow Anomaly Detector v1** | **PASS** | Live Edge submission $\to$ `200 OK` (`0.6883 > 0.5744`, `flagged: true`), truthful session-only notice |
| **10. Real .NET Early Governance Screening** | **PASS** | Live Edge submission $\to$ `200 OK` (`ReviewRequired`), persisted & visible on `/cases` |
| **11. Real PostgreSQL Dashboard & Case Dossiers** | **PASS** | Live Mode loads persisted assessments from PostgreSQL (`GET /dashboard`); zero demo leakage |
| **12. Automatic Outbox + Real Besu Blockchain Match** | **PASS** | Evaluation automatically queued to outbox $\to$ Go $\to$ Besu (`Block 8026`, tx `0x11c30f...`) $\to$ UI verified `Match` |
| **13. Automated Test Suites (.NET, Node, Pytest, Edge)** | **PASS** | 605 .NET unit tests, 19 Node tests, 412 Python ML tests, 17-route Edge UI test, 11-capability live Edge E2E test |

---

## 9. Genuine Known Boundaries & Limitations

1. **Academic Decision-Support Boundary:** System outputs provide decision support for authorized human officers. They do not constitute final legal lease approval, statutory decisions, or proof of criminal conduct.
2. **Synthetic Prospective & Held-Out ML Evaluations:** Complaint classifier `v2` prospective metrics ($n = 69$, Macro-F1 `0.9410`) and workflow anomaly `v1` held-out metrics ($n = 108$, Macro-F1 `0.8125`) are evaluated on synthetic datasets and do not represent real-world Sri Lankan departmental accuracy.
3. **Session-Only Workflow Anomaly Assessments:** Component 4 does not persist workflow-anomaly predictions in PostgreSQL; the UI explicitly informs the user that anomaly results are session-only and clears them on reload.
4. **Informational Boundary Screens (`/timing`, `/commissioner`, `/commissioner/review`, `/summary`):** Intentionally informational to respect the modular monolith boundary with Component 3 (`WorkflowGovernance`), which owns workflow stage clocks, officer queues, and task routing.

## 10. Governance Audit Hash Protocol Versions

- **AUDIT-V1 (historical):** Canonical payload fields are `version|record-id|engine-type|action|status|utc-timestamp|details-sha256`. The timestamp uses the original .NET UTC round-trip (`O`) representation, including all seven fractional-second digits. Existing rows are migrated only with `HashVersion = AUDIT-V1`; their stored digest, receipt, and blockchain transaction are never rewritten or replaced.
- **AUDIT-V2 (current default):** Uses the same deterministic field order, invariant UTF-8 SHA-256 representation, null normalization, and UTC conversion, but truncates the UTC timestamp to PostgreSQL microsecond precision before round-trip formatting. This ensures a newly persisted audit record recomputes to the same digest after reload.
- **Verification outcomes:** `Match` requires the recomputed record digest, stored receipt digest, and ledger digest to agree. `Mismatch` means available digest evidence disagrees. `LegacyUnverifiable` means an AUDIT-V1 stored digest agrees with the ledger but cannot be reproduced from PostgreSQL's microsecond-precision timestamp; it is not evidence of tampering. Unsupported versions fail closed.
- **Historical limitation:** PostgreSQL cannot recover a lost seventh fractional digit. Component 4 does not guess it, mutate historical rows, re-anchor historic records, delete ledger entries, or replace blockchain transactions.


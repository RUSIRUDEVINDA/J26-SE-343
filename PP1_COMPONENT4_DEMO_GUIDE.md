# Component 4 (Governance Intelligence) — PP1 Viva Demonstration Guide

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

## 5. Machine Learning (ML) Inference Demonstration

### Complaint Classification Model (TF-IDF + Logistic Regression):
- **Endpoint:** `POST http://localhost:5264/api/governance-intelligence/classify-complaint`
- **Request:**
  ```json
  {
    "complaintText": "Officer accepted payments directly without issuing government treasury receipts for the state lease parcel.",
    "applicantId": "APP-2026-DEMO",
    "caseId": "CASE-LK-2026-001"
  }
  ```
- **Response:**
  ```json
  {
    "predictedCategory": "Administrative / Procedural / Integrity",
    "probabilities": {
      "Administrative / Procedural / Integrity": 0.367,
      "Lease Revenue / Payment / Enforcement": 0.230,
      "Protected / Environmental Lease Misuse": 0.212,
      "Unauthorized Allocation / Transfer / Use": 0.191
    },
    "modelVersion": "v1"
  }
  ```
- **Interpretation:** Advisory machine classification across the 4-class taxonomy. Probabilities are advisory model outputs, not legal proof of an allegation.
### Workflow Anomaly Detector (Frozen Isolation Forest):
- **Endpoint:** `POST http://localhost:5264/api/governance-intelligence/predict-workflow-anomaly`
- **Request:** Trace of sequential events ending in `DS Final Notification`.
- **Flagged Example (Elevated Deviation / Rapid Handoffs):** `SIM-C4-2026-001.json` (5 events)
  - Anomaly Score: `0.6883`
  - Threshold: `0.5744`
  - Flagged for Review: `true` (score > threshold)
- **Non-Flagged Example (Standard Procedure):** `SL-CASE-0004` from synthetic baseline validation split (18 events)
  - Anomaly Score: `0.4772`
  - Threshold: `0.5744`
  - Flagged for Review: `false` (score <= threshold)
- **Interpretation:** Statistical trace deviation under Isolation Forest. Scores above threshold indicate unusual sequencing or rapid handoffs requiring human review; does not imply officer misconduct or legal invalidity.

---

## 6. Blockchain Trust Layer (Anchoring & Verification Evidence)

### End-to-End Authentic Lifecycle:
1. **Authentic Evaluation Execution:**
   Triggered genuine regulatory compliance evaluation through C# API (`POST /evaluate-compliance`), generating an authentic application audit record.
   - Generated Audit Record ID: `818f49f5-d954-4a4a-9305-a18133d1e2a2`
   - Action Name: `PP1-VIVA-AUTHENTIC-COMPLIANCE-EVALUATION`
   - Engine Type: `1` (`RegulatoryCompliance`)
   - Initial State before Anchoring: `AnchorStatus: "NotAnchored"`, `VerificationStatus: "NotAnchored"`
2. **Canonical Hash Computation (AUDIT-V1 in C#):**
   `AUDIT-V1|{RecordId}|{EngineType}|{ActionName}|{Status}|{TimestampUtc}|{DetailsHash}`
   - Computed Canonical Hash: `c670e36170fae262bd7974bffef4d46d54d8f608deb6b8330e22f35b127ee079`
3. **On-Chain Anchor Transaction:**
   Dispatched to `GovernanceAuditRegistry` on Besu node (Chain ID: 1337).
   - Smart Contract Address: `0x42699A7612A82f1d9C36148af9C77354759b210b`
   - Verified Transaction Reference: `0xa461c6f0a2188b2989d840d34e534623695897a59e6b75eca60ca3aa223e2c49`
   - Verified Block Number: `1891`
   - Anchored At UTC: `2026-10-10T17:52:28Z`
   - Anchor Status: `Anchored`
4. **On-Chain Verification Query:**
   - Calling `POST http://localhost:5264/api/governance-intelligence/audits/818f49f5-d954-4a4a-9305-a18133d1e2a2/verify`
   - Verification Status: `Match`
   - Verified At UTC: `2026-10-10T17:52:28.9293531Z`
   - Explanation: `"The current off-chain record digest matches the anchored blockchain ledger record."`
5. **Tamper Detection (Mismatch Verification):**
   When an off-chain record is modified or conflicting hash is presented:
   - Returns: `VerificationStatus: "Mismatch"`
   - Explanation: `"The current off-chain record does not match the anchored record version."`
   - Resubmitting conflicting hash on-chain reverts with smart contract error: `"Tamper protection: audit record ID was previously anchored with a different hash."`

---

## 7. Backup Demonstration Procedure (If Docker/Blockchain is Offline)

If Docker is not running or Besu is stopped:
1. The ASP.NET Core backend gracefully catches connection errors and sets `VerificationStatus: "Unavailable"` without crashing the primary business workflow.
2. The Outbox processor persists pending anchors to PostgreSQL table `governance_intelligence.governance_audit_anchor_outbox` for automatic retry once connectivity is restored.
3. In `governance-blockchain-service/.env`, setting `ENABLE_MOCK_LEDGER=true` enables the in-memory ledger fallback that executes the exact same contract semantics without Docker.

---

## 8. Viva Verification Status Summary

| Capability / Feature | Viva Status | Verification Evidence |
| :--- | :---: | :--- |
| **1. Real .NET Regulatory Compliance Engine** | **PASS** | Evaluated live; 24 findings generated; deterministic hash `fb70b740...` |
| **2. Real .NET Governance Conflict Engine** | **PASS** | Detected `AuthorityLevelInconsistency` across institutions |
| **3. Real .NET Governance Risk Engine** | **PASS** | Evaluated 13 indicators, computed score 15, classified level |
| **4. Real .NET Explainable Governance Engine** | **PASS** | Generated structured, ethical, neutral plain-language explanations |
| **5. Real .NET Institutional Consensus Engine** | **PASS** | Evaluated quorum, vetoes, and voting thresholds (`Blocked`) |
| **6. Real .NET Conditional Verification Engine** | **PASS** | Verified condition satisfaction and milestones (`FullySatisfied`) |
| **7. Real Python Complaint Classification ML** | **PASS** | FastAPI on port 8104; 4-class taxonomy inference verified live |
| **8. Real Python Workflow Anomaly ML** | **PASS** | Flagged trace (`0.6883`) and non-flagged trace (`0.4772` for `SL-CASE-0004`) verified |
| **9. Real PostgreSQL Persistence & Dashboard** | **PASS** | PostgreSQL 17; 101 audit records, 14 assessments read live |
| **10. Real Hyperledger Besu Blockchain Trust Layer** | **PASS** | 4-node QBFT network, smart contract `0x42699A...`, authentic record `818f49f5...` anchored at Block `1891`, verified `Match` |
| **11. Next.js Frontend Live Browser Walkthrough** | **PASS** | Playwright automated browser walkthrough verified all interactive screens; 13 screenshots captured in `scratch/browser_evidence/` |
| **12. Regression & Unit Test Suites** | **PASS** | 617 unit tests passed; Next.js lint passed (0 errors); build passed cleanly |

---

## 9. Genuine Known Boundaries & Limitations

1. **Academic Decision-Support Boundary:** System outputs provide decision support for authorized human officers. They do not constitute final legal lease approval, statutory decisions, or proof of criminal conduct.
2. **Closed-Set ML Classifier:** The complaint classification model operates on a frozen 4-class taxonomy in English; out-of-distribution inputs require officer review.
3. **Statistical Anomaly vs Legal Review:** Anomaly scores reflect sequencing deviations, not administrative misconduct.
4. **Decoupled Upstream Fixtures:** Upstream land parcels (Component 1) and workflow orchestrations (Component 3) are provided via validated synthetic fixtures rather than live peer-module dependencies, in full compliance with the modular monolith research boundary.


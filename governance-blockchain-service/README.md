# Governance Blockchain Service (Component 4 — Blockchain Trust Layer)

## 1. Architectural Overview

The **Governance Blockchain Service** is a dedicated, decoupled microservice within Component 4 (*AI-Driven Governance, Compliance and Trust Infrastructure*) of the State Land Management research platform.

### Authoritative Boundary
- **PostgreSQL**: Remains the **sole authoritative application datastore** for all land governance evaluations, regulatory checks, risk assessments, workflow events, and audit logs.
- **Hyperledger Besu (QBFT)**: Serves exclusively as a **tamper-evident integrity ledger**. It holds zero personally identifiable information (PII), zero applicant details, and zero confidential case records. Only normalized cryptographic SHA-256 digests and audit record identifiers are anchored on-chain.

```
+--------------------------------------------------------------------------+
|                        ASP.NET Core (.NET 8)                             |
|               (Component 4 - GovernanceIntelligence)                     |
|                                                                          |
|  1. Computes canonical audit hash (AUDIT-V1) via GovernanceAuditHasher    |
|  2. Persists Audit Record + Outbox Entry in PostgreSQL (atomic unit)     |
|  3. Background Outbox Processor dispatches anchor to Go Service          |
+-----------------------------------+--------------------------------------+
                                    | HTTP / JSON REST
                                    v
+--------------------------------------------------------------------------+
|                  Governance Blockchain Microservice (Go)                 |
|                                                                          |
|  - Validates request format (UUID, 64-char hex SHA-256)                  |
|  - Submits transaction to GovernanceAuditRegistry smart contract         |
|  - Translates EVM state into neutral anchor receipts and verification    |
+-----------------------------------+--------------------------------------+
                                    | Ethereum JSON-RPC (HTTP)
                                    v
+--------------------------------------------------------------------------+
|            Private Permissioned Hyperledger Besu Network                 |
|                      (QBFT Consensus, Chain ID 1337)                     |
|                                                                          |
|  Smart Contract: GovernanceAuditRegistry.sol                             |
|  - Stores: (recordId, recordHash, timestamp, submitter)                  |
|  - Role-Based Access Control (SUBMITTER_ROLE)                            |
|  - Enforces idempotency (same hash succeeds, different hash reverts)     |
+--------------------------------------------------------------------------+
```

---

## 2. Prerequisites & Tooling

| Component | Minimum Version | Purpose |
| :--- | :--- | :--- |
| **Go** | 1.22+ | Microservice implementation |
| **Node.js & npm** | Node 18+, npm 9+ | Hardhat Solidity testing and deployment |
| **Docker & Docker Compose** | Docker 24+, Compose v2 | Local Hyperledger Besu 4-node QBFT network |
| **.NET SDK** | .NET 8.0 SDK | ASP.NET Core backend & xUnit test suite |

---

## 3. Directory Layout

```
governance-blockchain-service/
├── cmd/
│   └── api/
│       └── main.go                 # Microservice entrypoint
├── internal/
│   ├── anchors/                    # Core anchoring & verification business orchestration
│   ├── api/                        # HTTP routing, JSON handlers, request validation
│   ├── besu/                       # go-ethereum RPC client & contract interaction
│   ├── config/                     # Environment variable configuration loader
│   └── contract/                   # Go bindings for GovernanceAuditRegistry
├── contracts/
│   └── GovernanceAuditRegistry.sol # Solidity smart contract
├── scripts/
│   └── deploy.js                   # Hardhat contract deployment script
├── network/
│   └── besu/
│       ├── genesis.json            # QBFT genesis block (Chain ID: 1337)
│       └── docker-compose.yml      # 4 validator nodes + RPC node
├── tests/
│   ├── api_test.go                 # Go API integration & unit tests
│   └── contracts/
│       └── GovernanceAuditRegistry.test.js # Hardhat contract tests
├── Dockerfile                      # Container build definition
├── hardhat.config.cjs              # Hardhat configuration
├── package.json                    # Node dependencies for contract testing
└── README.md                       # This documentation
```

---

## 4. Hyperledger Besu QBFT Network Setup

The development network uses a 4-validator QBFT (Quorum Byzantine Fault Tolerance) consensus setup with instant block finality.

### Starting the Network
```bash
cd network/besu
docker compose up -d
```

### Verifying Block Production
Query node 1's JSON-RPC endpoint:
```bash
curl -X POST --data '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}' \
  -H "Content-Type: application/json" http://localhost:8545
```
You will receive an increasing hexadecimal block number (e.g., `{"jsonrpc":"2.0","id":1,"result":"0x12"}`).

### Stopping the Network
```bash
docker compose down -v
```

---

## 5. Smart Contract Compilation & Deployment

### Run Unit Tests
Contract tests verify role-based permissions, idempotency, differing hash rejection, and match/mismatch verification:
```bash
cd governance-blockchain-service
npx hardhat test
```

### Deploy to Local Besu Network
Ensure the Besu network is running, then deploy:
```bash
npx hardhat run scripts/deploy.js --network besu
```
Output:
```
Deploying GovernanceAuditRegistry...
GovernanceAuditRegistry deployed to: 0x42699A7612A82f1d9C36148af9A7735a107FAca1
```
Copy the deployed contract address into your `.env` configuration.

---

## 6. Configuring & Running the Go Microservice

### Environment Configuration
Copy `.env.example` to `.env`:
```bash
cp .env.example .env
```
Key configuration parameters:
- `PORT`: HTTP port for REST API (default: `8082`).
- `BESU_RPC_URL`: JSON-RPC URL for Besu node (default: `http://localhost:8545`).
- `CONTRACT_ADDRESS`: Deployed `GovernanceAuditRegistry` contract address.
- `PRIVATE_KEY`: Submitter account private key with `SUBMITTER_ROLE` permissions (use development-only key in dev environments).
- `CHAIN_ID`: `1337` (matching QBFT `genesis.json`).

### Running Tests
```bash
go test -v ./...
```

### Running the Service
```bash
go run cmd/api/main.go
```
The service will start listening on `http://localhost:8082`.

---

## 7. ASP.NET Core Integration

### AppSettings Configuration
In `StateLandGovernance/src/Api/appsettings.json`:
```json
{
  "Blockchain": {
    "ServiceBaseUrl": "http://localhost:8082",
    "TimeoutSeconds": 15,
    "EnableAutomaticAnchoring": true,
    "OutboxPollingIntervalSeconds": 10,
    "MaxRetryAttempts": 5
  }
}
```

### Hashing & Canonicalization Protocol (`AUDIT-V1`)
C# is the **single authority** for canonicalization and SHA-256 hash generation.
The canonical audit representation format:
```
AUDIT-V1|{RecordId:D}|{(int)EngineType}|{NormalizedAction}|{NormalizedStatus}|{TimestampUtc:O}|{SanitizedDetailsHash}
```
Fields:
- `RecordId`: Lowercase 36-char GUID format (`D`).
- `EngineType`: Integer value corresponding to the governance engine enum.
- `NormalizedAction`: Uppercase, trimmed action descriptor.
- `NormalizedStatus`: Uppercase, trimmed status descriptor.
- `TimestampUtc`: ISO-8601 round-trip format (`O`).
- `SanitizedDetailsHash`: Deterministic SHA-256 digest of sanitized metadata keys and values (never contains raw PII).

---

## 8. On-Chain vs. Off-Chain Data Boundary

| Field / Attribute | Stored Off-Chain (PostgreSQL) | Stored On-Chain (Besu) | Rationale |
| :--- | :---: | :---: | :--- |
| **Audit Record ID** | Yes | Yes | Cross-system correlation index |
| **SHA-256 Digest (`RecordHash`)** | Yes | Yes | Immutable integrity anchor |
| **Timestamp (AnchoredAt)** | Yes | Yes | Ledger block timestamp |
| **Submitter Public Key** | No | Yes (EVM `msg.sender`) | Cryptographic proof of origin |
| **Transaction Hash / Block #** | Yes (Receipt table) | Yes (Blockchain state) | Audit trail verification |
| **Officer Identity / PII** | Redacted / Aggregated | **NEVER** | Data protection & privacy laws |
| **Applicant NIC / Name** | Strictly Protected | **NEVER** | Privacy & legal compliance |
| **Land Parcel Geometries** | Application DB | **NEVER** | Large binary data / privacy |
| **Full Legal / Conflict Findings** | Application DB | **NEVER** | Explainability details stay in DB |

---

## 9. Failure Handling & Asynchronous Resilience

If the Go service or Besu blockchain is unreachable:
1. **Primary Operation Unaffected**: Regulatory evaluation, risk assessments, and audit logging succeed normally and return `200 OK`.
2. **Outbox Persistence**: An entry is created in `governance_audit_anchor_outbox` with status `Pending`.
3. **Background Retries**: The `GovernanceAuditAnchorOutboxProcessor` scans pending records, transmits them to the Go service with exponential backoff, and marks them `Anchored` or `Failed` once retry limits are reached.
4. **Receipt State**: A neutral receipt is persisted with status `Unavailable` until the network restores and anchoring completes.

---

## 10. Development Credentials Notice

> [!CAUTION]
> The private keys, addresses, and genesis files provided under `network/besu/` and `.env.example` are **publicly known development-only identities** for local testing. Under no circumstances should these private keys or configurations be utilized in staging, testnet, or production environments.

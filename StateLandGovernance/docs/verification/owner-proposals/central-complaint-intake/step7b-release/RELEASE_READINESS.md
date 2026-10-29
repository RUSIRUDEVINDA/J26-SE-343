# C4 Step 7B-1 release preparation

2026-10-24, Asia/Colombo. This is a packaging and launcher-preparation record. It does not authorize or perform ordinary deployment.

## Artifacts

- Backend: .tmp/c4-step7a-api-final, .NET 8 publish output. PACKAGE_SHA256.json records all 88 files.
- Frontend: .tmp/c4-step7b-frontend, created from the successful Next 16.3.5 production build. It contains .next, package.json, package-lock.json, and next.config.ts. FRONTEND_SHA256.json records all 3,819 files. On a reviewed host, run npm.cmd ci followed by npm.cmd run start -- --hostname 127.0.0.1 --port 3000.
- API launcher: StateLandGovernance/tools/development/Start-C4Step7BApprovedApi.ps1. LAUNCHER_SHA256.json records its hash. It performs package, private-candidate, peer read-only, worker/sandbox, database identity, migration, attachment-table, and INSERT-right checks before it can start an API.

The source manifest is intentionally a reviewed release delta, not a claim that the dirty repository is a clean, self-contained source release. It includes the C4 Steps 1-7A paths, tests, API composition paths, the four approved C1 files, and the new launcher. It excludes private configuration, permission documents, backups, database dumps, generated output, model binaries, unrelated C1/C2/C3 work, and test fixtures. Existing Step 7A source provenance verifies 1,409 current C4/API source paths against its published hashes; no mismatch was found.

## Launcher validation

- The launcher accepted the retained isolated post-migration restore: 42 tables, 23 migrations, both required migration IDs, attachment table, and INSERT authority.
- It rejected the ordinary 41-table/21-migration database without starting a process.
- It rejected the retained isolated database missing the Step 2 migration.
- It rejected an isolated role without attachment INSERT authority. The temporary role and its grants were removed afterwards.
- After the final launcher hardening, the retained isolated post-migration restore again passed with both required attachment SELECT and INSERT privileges.

The launcher validates with PGOPTIONS=default_transaction_read_only=on. It does not call EF migration APIs, EnsureCreated, DDL, DML, or grants. API startup is deliberately unavailable under IsolatedTest; existing Step 7A isolated HTTP evidence remains the authoritative package startup and authorization proof. The source scan finds no C4 API-startup Database.Migrate or EnsureCreated call. The separately invoked account setup command remains outside startup.

## Remaining release qualification

The release manifest identifies the reviewed delta, but the repository remains a mixed dirty working tree (958 entries in the final inventory) and `git diff --check` reports pre-existing trailing whitespace in `EarlyGovernanceInvestigation.tsx`. Packaging authorization did not permit silently rewriting that unrelated source formatting. A deployment approver must accept this reviewed manifest as the source boundary, or require a clean, separately reviewed source snapshot before cutover.

## Ordinary deployment gate

Before separate activation approval, create a fresh backup/isolated restore if ordinary data changed; rerun this launcher against the post-migration isolated restore; stop only the identified ordinary API; apply the two reviewed forward SQL files in order and the single attachment SELECT/INSERT grant; verify OID 34144, 42 tables, 23 migrations and unchanged original data; then start one compatible API. Never run automatic migrations, destructive Down, restore over ordinary storage, or start a pre-Step-1 binary.

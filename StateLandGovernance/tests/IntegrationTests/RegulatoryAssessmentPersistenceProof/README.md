# Isolated regulatory persistence proof

This console integration proof uses the existing mapper, regulatory engine, PostgreSQL catalogue/store, report queries and actual controllers. It never loads repository .env, starts the API host, contacts C1/C3, calls ML/Groq or starts a worker. Its temporary HTTP host uses a test-only authentication scheme; this is not production authentication code.

Requires .NET8 and PostgreSQL binaries. Use a newly initialized loopback-only PostgreSQL cluster and fresh database. The proof refuses non-loopback hosts, ports below55000, database names without c4_regulatory_test_ and databases with applied migrations. It never resets or deletes test/ordinary data. The task's cluster on57643 was stopped after validation; its ignored .tmp data is retained.

Run from repository root. Each step is one command; choose a fresh ignored directory/database if these already exist.

1. `& 'C:/Program Files/PostgreSQL/18/bin/initdb.exe' -D .tmp/regulatory-proof-fresh -U regulatory_test_admin --auth=trust --encoding=UTF8 --locale=C`
2. `& 'C:/Program Files/PostgreSQL/18/bin/pg_ctl.exe' -D .tmp/regulatory-proof-fresh -l .tmp/regulatory-proof-fresh.log -o '-p 57643 -h 127.0.0.1' start`
3. `& 'C:/Program Files/PostgreSQL/18/bin/createdb.exe' -h 127.0.0.1 -p 57643 -U regulatory_test_admin c4_regulatory_test_fresh`
4. `$env:C4_REGULATORY_ISOLATED_TEST_CONNECTION='Host=127.0.0.1;Port=57643;Database=c4_regulatory_test_fresh;Username=regulatory_test_admin;Options=-c default_transaction_read_only=off'`
5. `dotnet build StateLandGovernance/tests/IntegrationTests/RegulatoryAssessmentPersistenceProof/RegulatoryAssessmentPersistenceProof.csproj -o .tmp/regulatory-proof`
6. `dotnet .tmp/regulatory-proof/RegulatoryAssessmentPersistenceProof.dll`
7. `& 'C:/Program Files/PostgreSQL/18/bin/pg_ctl.exe' -D .tmp/regulatory-proof-fresh -m fast stop`

The short output path/DLL invocation avoids Windows apphost path-length failures. Trust authentication is for this fresh loopback-only test cluster, never ordinary databases. Starting/stopping a cluster may require local process permission. Preserve data; do not use database/cluster deletion as a retry mechanism.

catalogue-input.json contains public mapped rule-definition INPUTS captured in the existing workspace; no expected predictions or approval metadata is manufactured. The proof seeds only its isolated database, loads them through PostgresComplianceRuleCatalogue and invokes the actual regulatory engine. One copied test row's version is varied in isolated storage solely to test catalogue-digest/version separation; production rules and code are unchanged. Kiulakale input comes from the existing prepared package, never a real case/snapshot.

Checks cover the full migration chain with pending complaints predecessor, historical read compatibility before/after provenance migration, no historical identity backfill, atomic evaluation/audit/outbox/provenance save and injected rollback, concurrent idempotency and input/version conflicts, creator ownership/cross-actor denial, supplied C3 denial, actual mapped input/catalogue hashes, later catalogue versions, readiness against read-only sessions, actual HTTP submission/retrieval and creator spoofing. Outbox entries remain Pending; no anchoring worker is instantiated. See the saved29-check evidence in docs/verification/owner-proposals/live-regulatory-assessments.

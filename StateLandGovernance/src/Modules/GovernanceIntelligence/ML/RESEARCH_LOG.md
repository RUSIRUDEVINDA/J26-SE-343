# GovernanceIntelligence ML Research Log

## 2026-10-07 — v1 artifact validation and Experiment 4 report correction

- Validated the existing v1 joblib and metadata as an immutable pair; neither file was rewritten.
- Confirmed the fitted pipeline contains `BoilerplateStripper`, `TfidfVectorizer`, and `LogisticRegression`, is fitted, and exposes the fixed four-class taxonomy in the order recorded by model metadata.
- Compared 19 stable pipeline parameters with Experiment 4 Arm A run2 metadata.
- Revalidated the original 200-row dataset hash separately from the 530-row candidate dataset hash, then matched baseline IDs, labels, and raw canonical text against saved experiment evidence.
- Recorded the limitation that joblib does not preserve an independently verifiable historical snapshot of custom transformer source code; deserialization uses the currently installed class source.
- Recomputed the Experiment 4 comparison from one-to-one aligned OOF predictions. Arm A accuracy was 0.77 with macro-F1 0.768457337101826; Arm B accuracy was 0.76 with macro-F1 0.760147960006451. Nine records were helped and eleven were hurt.
- Validated the saved paired-transition CSV against recomputed transitions and derived baseline training counts of 141–175 from saved manifests and run metadata.

## 2026-10-07 — Internal HTTP prediction boundary

- Added a FastAPI application that loads the validated v1 model, metadata, and validation sidecar once during application lifespan startup.
- Reused `predict_v1.load_versioned_artifact` and `predict_v1.predict_complaint`; no preprocessing, validation, or prediction logic was duplicated and no model was fitted.
- Added loopback-first `/health` and `/predict` endpoints for a future .NET backend caller, with no browser CORS, persistence, authentication, or public binding.
- Added a configurable 10,000-character default operational input limit, strict nonblank string validation, and sanitized HTTP errors that do not echo complaint content or local artifact paths.
- Added focused HTTP-boundary tests using small stubs for startup, single loading, payload validation, case-ID separation, prediction output, and error sanitization.

## 2026-10-07 — .NET complaint-classifier client boundary

- Added an Application-layer advisory classification contract and the authoritative four-category taxonomy without introducing HTTP concerns into Application.
- Added an Infrastructure typed HTTP client for `POST /predict`, explicit snake_case transport mappings, validated base URL and timeout options, and GovernanceIntelligence composition-root registration.
- Added local input checks aligned to Python's 10,000-Unicode-scalar limit and strict response validation for taxonomy, probability shape and values, case-ID preservation, and advisory/closed-set notes.
- Classified expected failures as caller input, upstream HTTP 422, service unavailability, timeout, other upstream failure, or incompatible response; caller cancellation remains cancellation.
- Kept health checks out of the Application interface and out of startup because prediction is the only workflow capability needed for this milestone.
- Verified the adapter with controlled HTTP tests and a real loopback smoke call from the registered .NET client to the existing FastAPI v1 service; the temporary harness and service process were removed afterward.

## 2026-10-08 — .NET client URL hardening and API host compilation repair

- Hardened complaint-classifier base URL validation to reject query strings, fragments, and embedded credentials before constructing the typed client.
- Replaced raw trailing-slash concatenation with URI-aware base-address normalization that preserves root and prefixed service paths for the relative `predict` route.
- Added focused DI/client tests for rejected URL components and outgoing root/prefixed prediction request URIs.
- Repaired API Swagger document registration by configuring both existing documents inside the installed `AddSwaggerGen` callback and resolving `OpenApiInfo` from the supported namespace.
- Verified 47 focused classifier tests, including 15 DI tests, and zero-warning Infrastructure/API project builds.
- Started the development API on loopback and received HTTP 200 from its existing external Swagger document, then repeated a successful real .NET-to-FastAPI v1 prediction smoke test; all temporary processes and harness files were removed.

## 2026-10-08 — .NET classification orchestration and persistence

- Added an Application command handler that validates complaint and case inputs, calls the existing classifier boundary with caller cancellation, and persists only successful predictions through an Application-owned store contract.
- Added an immutable assessment snapshot containing the original complaint text and case reference, model version, exact closed-set category, four class probabilities, advisory notes, a generated assessment identifier, and a UTC assessment timestamp.
- Added a scoped PostgreSQL store, explicit EF Core entity mapping, and an additive migration in the existing `governance_intelligence` schema; no cross-module foreign key, Case ID uniqueness constraint, or automatic retry/deduplication was introduced.
- Added focused orchestration, DI lifetime/configuration, and dedicated-PostgreSQL round-trip tests. The PostgreSQL test requires `COMPONENT4_TEST_POSTGRES_CONNECTION` to name a database beginning with `component4_screening_test_` and deletes only its own assessment row.
- Verified 63 complaint-classification tests, 25 affected dependency-injection tests, and zero-warning Infrastructure/API builds. The real PostgreSQL round trip remains pending because the dedicated test connection was not configured; the ordinary development database was not used and the migration was not applied.

## 2026-10-08 — Real PostgreSQL and live classifier persistence verification

- Provisioned a uniquely named disposable database on the existing loopback PostgreSQL 17 service using locally configured credentials supplied only to the verification processes.
- Applied the real GovernanceIntelligence EF Core migration chain with `Database.MigrateAsync` and confirmed migration `20261008062258_GovernanceIntelligence_AddComplaintClassificationAssessments` in EF migration history.
- Expanded the PostgreSQL integration coverage for high-precision probability round trips, valid zero/one boundaries, database rejection of invalid ranges and sums, and distinct historical assessments sharing one Case ID.
- The first real round trip exposed precision loss from direct `double`-to-`decimal` casting. Changed persistence conversion to parse the invariant round-trip representation before PostgreSQL `numeric(18,17)` storage; all six PostgreSQL cases then passed.
- Ran a temporary live chain through the DI-registered `ClassifyComplaintCommandHandler`, typed HTTP client, validated FastAPI v1 model, PostgreSQL store, and a fresh-scope reload. Assessment identity, original fictional text and case context, model/category, notes, and all four probabilities agreed within storage precision.
- Deleted the smoke assessment, stopped the task-owned FastAPI process, dropped the disposable database, and removed the temporary harness and logs. No normal development database, model artifact, dataset, container, or volume was changed.

## 2026-10-08 — Early-governance Commissioner-referral preparation

- Inspected the seven-indicator screening rules and found only `Clear`, `ReviewRequired`, and `InsufficientInformation` outcomes; no existing rule authorizes an automatic Land Commissioner referral. The production policy therefore records `Clear` as non-referral and leaves both other outcomes unresolved without creating an intent.
- Confirmed Component 3 has workflow identities and lifecycle concepts but no published command, event, or repository operation that can atomically end the exact current workflow run and start one separate Commissioner review process.
- Added a Component 4 durable referral-intent model, atomic assessment-plus-intent persistence, correlation uniqueness, delivery attempt/failure/acknowledgement state, bounded case-history retrieval, and an unconfigured handoff adapter that cannot claim a workflow transition.
- Documented the proposed idempotent Component 3 boundary, including exact case/run matching, stale-run handling, retry semantics, and the requirement to return the distinct Commissioner review process reference only after the transition is confirmed.
- Added orchestration, policy, failure/retry, stale-run, persistence, migration, history, database-constraint, and DI coverage. All 515 non-PostgreSQL GovernanceIntelligence tests passed, and Infrastructure/API builds completed with zero warnings and errors.
- PostgreSQL referral tests compile but were not executed: the local PostgreSQL 17 service is running, while neither `COMPONENT4_TEST_POSTGRES_CONNECTION`, an application development connection, a user-secrets store, nor a PostgreSQL password file is available in this session. No database was created or modified.

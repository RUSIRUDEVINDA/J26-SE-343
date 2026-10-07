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

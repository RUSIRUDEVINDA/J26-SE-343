# Workflow anomaly data and feature boundary

This isolated package validates the extracted `synthetic_workflow_v1` release
and computes its exact nine completed-trace features. It does not train or fit a
model, and it does not modify files under `data/`.

Run from `StateLandGovernance/src/Modules/GovernanceIntelligence/ML`:

```powershell
python -m anomaly_detection.cli validate
python -m anomaly_detection.cli reconcile
python -m anomaly_detection.cli train-baseline
python -m anomaly_detection.cli verify-baseline
python -m anomaly_detection.cli evaluate-test
python -m anomaly_detection.cli verify-bundle
python -m anomaly_detection.cli infer --input-json anomaly_detection/examples/complete_synthetic_trace.json
python -m pytest anomaly_detection/tests -q
```

If `python` is not the intended project interpreter, replace it with the full
interpreter path or the repository's configured launcher.

Validation verifies every extracted file listed in `checksums.json`; it cannot
independently verify the recorded source ZIP hash when that ZIP is unavailable.
The validator rejects missing/duplicate keys, non-contiguous event sequences,
invalid or timezone-aware timestamps, backward time, incomplete traces,
non-finite values, model-input leakage, split overlaps, row misalignment, and
any use of quarantined cases.

Input row order is event-sequence order. No timestamp sorting, timestamp repair,
UTC assignment, or business-calendar inference occurs. Raw activity strings are
preserved; canonical activity removes only a terminal `(Repeated)`,
`(Re-submission)`, or `(Correction Requested)` suffix. Resource identity is the
pair `(institution, resource)`.

Prepared floating values are reconciled with absolute tolerance `1e-9` and
relative tolerance `1e-12`. These tolerances cover decimal serialization of
unrounded binary floats; they do not accept the originals' two-decimal rounding.

`reference-normal` remains a generator-designated scenario label, not
independently verified normality. `routing_config.json` remains simulation
configuration, not Sri Lankan law. Generator lineage is explicitly
`not_supplied`; the validator does not infer parent or family relationships.

## Frozen synthetic-only baseline

`train-baseline` is milestone 2's single, non-overwriting experiment. It fits
only `X_train.csv` (240 generator-designated reference cases) with no learned
preprocessing and the frozen `IsolationForest` parameters:

```text
n_estimators=200, max_samples="auto", max_features=1.0,
contamination="auto", bootstrap=False, random_state=42
```

It defines `anomaly_score = -model.score_samples(X)`, so larger scores mean
more statistically unusual, not more probable. It does not use
`IsolationForest.predict()` as a policy. The threshold is the validation
reference-only 95th percentile from `numpy.quantile(..., method="linear")` and
flags only when `anomaly_score > threshold`; equality is not flagged.

The default output path is `runs/isolation_forest_baseline_v1/` and any existing
output path is rejected. The run stores the model, SHA-256, frozen contract and
threshold, validation-only scores and predictions, metrics (including undefined
metric states), dependency/input hashes, and limitations. It never scores the
test split. `verify-baseline` reloads the local saved artifact and checks that
its validation scores and strict flags reproduce.

The declared 5% threshold target is only a synthetic experiment choice. It is
not a legal rule, operational SLA, calibrated probability, or guaranteed
false-positive rate. This experiment is neither a corruption detector nor a
validated Sri Lankan departmental model.

## Held-out test evaluation and local inference

`evaluate-test` verifies all milestone-2 hashes, model parameters, feature
order, split references, threshold evidence, and the data boundary before it
scores the reserved 108-row synthetic test matrix once. It does not refit,
tune, change the threshold, or alter split assignments. It creates two new,
non-overwriting directories:

```text
evaluations/synthetic_held_out_test_v1/
bundles/workflow_anomaly_model_v1/
```

The bundle links the model, metadata, frozen feature/threshold contract, and
held-out evaluation evidence by relative trusted-local paths and SHA-256 hashes.
It is verified by `verify-bundle` before local inference deserializes the
saved model.

For `infer`, provide only an input JSON object with a nonblank `case_id` and an
ordered `events` array. Every event requires a positive integer `event_seq` and
nonblank `activity`, `institution`, `resource`, and naive ISO-8601 `timestamp`.
The supplied order is authoritative: events are never sorted or repaired. The
trace must be complete, with one final `DS Final Notification` event last; a
partial/in-flight trace is rejected. Model or threshold paths in payloads are
rejected because trusted paths live in `bundles/workflow_anomaly_model_v1/
local_inference_config.json`.

Inference returns the nine values, score, frozen threshold, and strict flag.
The score is not probability/confidence and no feature-causation explanation is
returned. The included example is fictional synthetic input, not an evaluation
case or real departmental record.

## Internal HTTP inference boundary

The existing GovernanceIntelligence FastAPI process also exposes
`POST /workflow-anomaly/predict`. It uses the same complete-trace JSON contract
as the local `infer` command and calls the same validated feature extraction and
preloaded-bundle inference function. The complaint-classifier `POST /predict`
route and its response remain separate.

Start the service from the `ML/` directory on loopback:

```powershell
python -m uvicorn src.http_service:app --host 127.0.0.1 --port 8104
```

In another PowerShell session, submit the documented fictional trace:

```powershell
$body = Get-Content -Raw 'anomaly_detection/examples/complete_synthetic_trace.json'
Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8104/workflow-anomaly/predict' -ContentType 'application/json' -Body $body | ConvertTo-Json -Depth 5
```

The request object accepts only `case_id` and `events`. Each event accepts only
`event_seq`, `activity`, `institution`, `resource`, and `timestamp`. The response
contains `case_id`, `model_version`, `anomaly_score`, `threshold`, `flagged`, the
nine `feature_values`, and the synthetic-reference `advisory_note`. The score is
not a probability or confidence value; the strict policy remains
`anomaly_score > threshold`.

At application startup, the service verifies and loads the trusted frozen
bundle once. `GOVERNANCE_ANOMALY_BUNDLE_CONFIG` optionally selects its trusted
local configuration; artifact paths are never accepted in request payloads.
`GOVERNANCE_ANOMALY_MAX_REQUEST_BYTES` defaults to `262144`, and
`GOVERNANCE_ANOMALY_MAX_EVENTS` defaults to `500`. Both positive-integer limits
are operational safeguards, not anomaly rules, legal rules, or model features.

Loopback binding limits network exposure during local development but is not
authentication. Disabled browser CORS is also not authentication. Any remote
deployment still requires an explicit service-to-service authentication,
authorization, and transport-security design outside this milestone.

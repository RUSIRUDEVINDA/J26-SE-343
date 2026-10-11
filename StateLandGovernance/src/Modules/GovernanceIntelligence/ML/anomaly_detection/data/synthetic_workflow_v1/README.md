# Synthetic workflow dataset v1

Status: prepared for synthetic-only, completed-case Isolation Forest experiments. No model has been trained. This is not actual departmental data or a production validation dataset.

## Population
Original: 500 cases / 8242 events. Prepared: 456 cases / 7422 events. Quarantined: 44 cases / 820 events. All original bytes are preserved in originals/.

36 cases have backward timestamps in event-sequence order. Another 8 have post-notification events with final notification not last. These 44 cases are excluded, not repaired or relabelled. Post-notification events may be meaningful in another protocol, but cannot silently be treated as completed application traces here. Do not sort away chronological defects or invent replacement dates.

29 supplied feature cells across 19 cases disagree with calculations in supplied event_seq order beyond 2-decimal rounding tolerance. Prepared features are recomputed, never copied. Audit mismatch figures use raw activity/resource semantics; canonicalization and institution-scoped resources are separately documented definition changes.

All 19 raw-feature mismatch cases fall in quarantine. Among accepted cases, canonical activity definitions change unique/repeated counts in 33 cases; institution-scoped resource identity changes handoffs in 120 cases and distinct-resource counts in 299. These are deliberate feature-definition changes, not claims that the original raw-name calculations were arithmetically wrong. Raw identifiers and activities remain intact. There are no exact duplicate prepared numerical feature vectors.

## Files and use
- prepared/event_log.csv: accepted events with unchanged timestamps, sequence and raw text; derived canonical activity and explicit synthetic provenance.
- prepared/case_features.csv: case key plus nine recomputed numerical features.
- prepared/case_catalog.csv: all 500 case metadata rows, scenario labels, quality flags and unknown lineage.
- splits/X_train.csv: numerical inputs only, 240 synthetic reference cases. Do not train on y, keys, IDs or scenario metadata.
- splits/X_validation.csv and X_test.csv: 108 rows each; keys and synthetic labels stored separately in matching row order.
- splits/split_manifest.csv: fixed assignments for all 500 cases, including quarantine.
- feature_contract.json: exact definitions and excluded columns.
- audit/: row-level defects, quarantine, feature mismatches and counts.
- originals/routing_config.json: user-supplied simulation settings preserved, NOT a verified legal routing specification.

Use validation for parameter/threshold decisions and test once after freezing them. Do not use the 20% injected source prevalence as a real-world contamination estimate. Fit any learned preprocessing on train only. Keep raw activity strings and ID digits out of the model: IDs encode scenario ranges and several activities explicitly name injections. Scoring supports complete traces only; online/prefix scoring requires a separate protocol.

## Interpretation limits
Reference-normal means generator-designated reference, not independently verified normality. Anomaly labels describe controlled synthetic scenarios, not corruption or legal noncompliance. Evaluation metrics will measure this simulator only. All 400 reference cases have no repeated raw activities, which can make injected repetition artificially easy. Dataset has 5099 events outside 08:00–17:00 and 2365 weekend events; do not interpret these as officer attendance violations. No business calendar or timezone was supplied.

The field legal_route equals land_purpose throughout; these are four simulation scenario categories, not verified legal bases. Higher-approval/registration probabilities are simulation parameters, not law. Conditional activities match this supplied config, but domain validity remains unverified. Resource identity semantics are incomplete; pair institution with resource when counting changes.

No generator, generation seed, latent family ID or parent-case mapping was supplied. We found no exact relative-time trace duplicates and no identical first-two-event signatures. This does NOT prove independent families. If generator lineage becomes available, rebuild grouped splits before claims. No paired helped/hurt comparison against original parent cases is supported.

After quarantine there are only 1 repeated-correction case and 3 excessive-handoff cases. Repeated-correction is validation-only; test has no coverage of that type. Do not claim reliable detection across all five perturbation types. The 44 excluded cases should be regenerated from the original generator with valid insertion timing, preserving parent/family IDs, before broader evaluation. No human-review approval has been assumed.

## Reproduce
Run: python prepare_synthetic_v1.py INPUT_ZIP NEW_OUTPUT_DIRECTORY
Python 3.10+ standard library only. Existing output directories are rejected. This script is specific to the audited 500-case input; it does not silently accept changed populations. Split seed: wolf-synthetic-v1-20261008. Source hashes and output checksums are recorded.

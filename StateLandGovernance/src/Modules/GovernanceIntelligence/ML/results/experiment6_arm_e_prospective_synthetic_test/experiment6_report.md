# Experiment 6 — Frozen Arm E Prospective Synthetic Evaluation

> Synthetic prospective evaluation only. These results are not real-world accuracy. The candidate was not deployed.

## Review validation

- Reviewed families/records before filtering: 25 / 75
- Eligible families/records: 23 / 69
- Excluded families/records: 2 / 6
- Eligible: SYN-FAM-005, SYN-FAM-008, SYN-FAM-012, SYN-FAM-014, SYN-FAM-015, SYN-FAM-020, SYN-FAM-022, SYN-FAM-029, SYN-FAM-035, SYN-FAM-048, SYN-FAM-052, SYN-FAM-055, SYN-FAM-063, SYN-FAM-069, SYN-FAM-072, SYN-FAM-075, SYN-FAM-081, SYN-FAM-082, SYN-FAM-084, SYN-FAM-086, SYN-FAM-087, SYN-FAM-093, SYN-FAM-097
- Excluded: SYN-FAM-010, SYN-FAM-024

## Final training population

200 real baseline records plus 225 records from 75 approved development synthetic families. Real weights were 1.0 and synthetic variant weights were 1/3. Source-derived and prospective records were excluded.

## Frozen configuration

- Canonical configuration SHA-256: `a71710157a029de2df6a6329e276ceec12fc44433799960f862778d89fe68376`
- BoilerplateStripper → FeatureUnion(established word TF-IDF, char_wb 3–5 TF-IDF) → LogisticRegression
- Random seed: 42; frozen four-class order preserved.

## Record-level results

- Support: 69; correct: 65; incorrect: 4
- Accuracy: 0.942028986
- Balanced accuracy: 0.934523810
- Macro precision / recall / F1: 0.950595238 / 0.934523810 / 0.940976005
- Weighted F1: 0.942951540
- Wilson 95% accuracy interval: [0.860207566, 0.977227611]

## Family-level results

- Eligible families: 23
- Majority accuracy / macro-F1: 1.000000000 / 1.000000000
- All three variants correct: 19
- 0/1/2/3 correct distribution: {'0': 0, '1': 0, '2': 4, '3': 19}
- Variant disagreements: 4; majority ties: 0

## Per-class results

- Administrative / Procedural / Integrity: support 21, precision 1.000000000, recall 0.952380952, F1 0.975609756
- Lease Revenue / Payment / Enforcement: support 9, precision 1.000000000, recall 0.888888889, F1 0.941176471
- Unauthorized Allocation / Transfer / Use: support 18, precision 0.850000000, recall 0.944444444, F1 0.894736842
- Protected / Environmental Lease Misuse: support 21, precision 0.952380952, recall 0.952380952, F1 0.952380952

## Integrity, decision, and limitations

- Protected input preservation: True
- No training/test ID, family, or exact-text overlap; no additional model selection or tuning was performed.
- The candidate is unpromoted and stored only inside this Experiment 6 directory.
- Both development and prospective synthetic data share a common generator origin, so performance may reflect generator regularities and must not be treated as real-world accuracy.

## Verdict

**ARM E PASSED PROSPECTIVE SYNTHETIC EVALUATION — PROMOTION REVIEW REQUIRED**

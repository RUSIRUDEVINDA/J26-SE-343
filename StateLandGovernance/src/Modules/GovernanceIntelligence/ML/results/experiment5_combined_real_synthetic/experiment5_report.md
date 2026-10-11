# Experiment 5: Leakage-Safe Combined Real + Synthetic Development Evaluation

> Development evidence only. This is not a final independent or prospective test, and no model was promoted.

## Validated populations

- Real/source-confirmed baseline: 200
- Approved synthetic families/records: 75 / 225
- Excluded source-derived candidates: 30
- Excluded unreviewed families/records: 25 / 75

## Split strategy and leakage checks

The exact frozen Experiment 4 Arm A real folds and Combined_Group assignments were reused. Approved synthetic families were label-stratified into five deterministic folds with seed 42; all three variants stay together. For each fold, neither real Combined_Group nor Synthetic_Family_ID crosses training/evaluation.

## Arm metrics

| Arm | Population | Support | Accuracy | Balanced accuracy | Macro-F1 | Weighted-F1 |
|---|---:|---:|---:|---:|---:|---:|
| ARM_A | real | 200 | 0.770000 | 0.770000 | 0.768457 | 0.768457 |
| ARM_A | synthetic | 225 | 0.831111 | 0.838356 | 0.831350 | 0.830737 |
| ARM_A | combined | 425 | 0.802353 | 0.803497 | 0.801331 | 0.801381 |
| ARM_B | real | 200 | 0.790000 | 0.790000 | 0.788321 | 0.788321 |
| ARM_B | synthetic | 225 | 0.942222 | 0.941822 | 0.943211 | 0.942367 |
| ARM_B | combined | 425 | 0.870588 | 0.868873 | 0.869819 | 0.870167 |
| ARM_C | real | 200 | 0.760000 | 0.760000 | 0.759572 | 0.759572 |
| ARM_C | synthetic | 225 | 0.977778 | 0.979266 | 0.978667 | 0.977709 |
| ARM_C | combined | 425 | 0.875294 | 0.873874 | 0.874815 | 0.875262 |
| ARM_D | real | 200 | 0.775000 | 0.775000 | 0.773927 | 0.773927 |
| ARM_D | synthetic | 225 | 0.946667 | 0.948097 | 0.946869 | 0.946294 |
| ARM_D | combined | 425 | 0.865882 | 0.864777 | 0.865008 | 0.865430 |
| ARM_E | real | 200 | 0.800000 | 0.800000 | 0.798904 | 0.798904 |
| ARM_E | synthetic | 225 | 0.964444 | 0.964220 | 0.965699 | 0.964549 |
| ARM_E | combined | 425 | 0.887059 | 0.885262 | 0.886347 | 0.886814 |

## Synthetic family metrics

| Arm | Majority accuracy | Majority macro-F1 | All 3 correct | 0/1/2/3 correct distribution |
|---|---:|---:|---:|---|
| ARM_A | 0.920000 | 0.922814 | 47 | {'0': 0, '1': 10, '2': 18, '3': 47} |
| ARM_B | 0.973333 | 0.972866 | 64 | {'0': 0, '1': 2, '2': 9, '3': 64} |
| ARM_C | 1.000000 | 1.000000 | 70 | {'0': 0, '1': 0, '2': 5, '3': 70} |
| ARM_D | 0.973333 | 0.974271 | 65 | {'0': 0, '1': 2, '2': 8, '3': 65} |
| ARM_E | 0.986667 | 0.987776 | 68 | {'0': 0, '1': 1, '2': 6, '3': 68} |

## Paired comparisons versus Arm A

### ARM_B

- real: helped 7, hurt 3, net 4, prediction changes 10.
- synthetic: helped 28, hurt 3, net 25, prediction changes 32.
- combined: helped 35, hurt 6, net 29, prediction changes 42.

### ARM_C

- real: helped 8, hurt 10, net -2, prediction changes 21.
- synthetic: helped 35, hurt 2, net 33, prediction changes 38.
- combined: helped 43, hurt 12, net 31, prediction changes 59.

### ARM_D

- real: helped 6, hurt 5, net 1, prediction changes 12.
- synthetic: helped 27, hurt 1, net 26, prediction changes 30.
- combined: helped 33, hurt 6, net 27, prediction changes 42.

### ARM_E

- real: helped 12, hurt 6, net 6, prediction changes 18.
- synthetic: helped 34, hurt 4, net 30, prediction changes 39.
- combined: helped 46, hurt 10, net 36, prediction changes 57.

## Selection result

Selected development candidate: `arm_e`.
Passing these checks does not authorize deployment.

## Limitations

This is development cross-validation over the existing real baseline and researcher-reviewed synthetic scenarios. Synthetic review is not an independent prospective test, Arm E is development-oriented, and the same folds are used for comparison/selection. The 30 pending source-derived candidates and 25 unreviewed synthetic families were not evaluated.

## Verdict

**EXPERIMENT 5 CANDIDATE IDENTIFIED — PROSPECTIVE TEST STILL REQUIRED**

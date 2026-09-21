# Component 4 — GovernanceIntelligence Text Classifier

## Research Context

This ML module belongs to:

**Component 4 — AI-Driven Governance, Compliance and Trust Infrastructure**
Subcomponent: *Early Governance and Post-Workflow Anomaly Detection*

Research project: *An Integrated Architectural Framework for a Decentralized and Intelligent Land Administration Ecosystem*

---

## Classifier Purpose

This classifier predicts the **reported governance-issue category** of an English
state-land lease complaint written in the Sri Lanka context.

The output is **advisory classification intelligence** for subsequent human/governance processing.

**The classifier does NOT:**
- Determine whether an allegation is true
- Determine guilt
- Determine legal liability
- Determine corruption as a proven fact
- Approve or reject lease applications
- Issue disciplinary or administrative action
- Produce legally binding decisions

---

## Model Architecture

```
English complaint text
        ↓
TF-IDF  (unigrams + bigrams, sublinear_tf=True, English stop-words)
        ↓
Logistic Regression  (L2, C=1.0, class_weight='balanced', max_iter=2000)
        ↓
4-class governance prediction
```

**No pretrained language model or pretrained classifier is used.**
This classifier is trained entirely from the project dataset.

---

## Fixed Four-Class Taxonomy

| # | Label |
|---|-------|
| 1 | Administrative / Procedural / Integrity |
| 2 | Lease Revenue / Payment / Enforcement |
| 3 | Unauthorized Allocation / Transfer / Use |
| 4 | Protected / Environmental Lease Misuse |

Do not add, merge, or rename classes without revising the research protocol.

---

## Input / Target / Grouping Fields

| Role | Field |
|------|-------|
| **Text input** | `Canonical_English_Text` |
| **Target** | `ML_Label_4Class` |
| **Grouping** (CV only) | `Source_Group_ID` |

---

## Excluded Metadata

The classifier uses **text only**. The following fields are *never* used as model input features:

- `Research_ID`, `Incident_Title_EN`, `Original_Language`
- `Original_Label_6Class`, `Issue_Subtype`, `District`
- `State_Authority`, `Source_URL`, `Verification_Status`
- `Verification_Basis`, `Gold_Label_Status`, `Current_Status`
- `CV_Fold`, `Source_Group_ID`, `Incident_Group_ID`
- `Notes`, any officer/applicant PII fields

`Source_Group_ID` is allowed **only** for cross-validation grouping — it is never a classifier feature.

---

## Data Leakage Prevention

Cross-validation uses **StratifiedGroupKFold** with `Source_Group_ID` as the grouping key.
This prevents records from the same audit source (e.g., the same Auditor General report)
from appearing in both the training and test portions of any fold.

TF-IDF is fitted **only on the training portion** of each fold. It is never fit on the full dataset before cross-validation begins.

---

## Group-Aware Cross-Validation

```python
StratifiedGroupKFold(n_splits=5, shuffle=True, random_state=42)
groups = Source_Group_ID
```

Each fold builds a completely fresh pipeline. Out-of-fold (OOF) predictions are collected
across all folds to compute overall metrics.

---

## Metrics

| Metric | Description |
|--------|-------------|
| **Macro-F1** | Primary performance metric |
| Accuracy | Overall fraction correct |
| Macro-Precision | Mean precision across classes |
| Macro-Recall | Mean recall across classes |
| Weighted-F1 | F1 weighted by class support |
| Per-class: Precision, Recall, F1, FPR | One row per class |

FPR formula: `FP / (FP + TN)` — **not** `1 - Precision`.

---

## Baseline Comparison

A `DummyClassifier(strategy='most_frequent')` is evaluated under the same
cross-validation philosophy. This establishes whether the trained model
meaningfully exceeds a trivial baseline.

---

## Evaluation Experiments

Two evaluation experiments are maintained. They do NOT replace each other.

---

### Experiment 1 — Source-Group-Aware Baseline

**Script:** `src/evaluate.py`

**Grouping:** `groups = Source_Group_ID`

Prevents leakage from records that share the same source document (e.g., the same Auditor General report).
Does not detect records with shared narrative template language that happen to have different Source_Group_IDs.

| Metric | Value |
|--------|-------|
| **Accuracy** | 0.7450 |
| **Macro-F1** | **0.7402** |
| Weighted-F1 | 0.7402 |
| Misclassified | 51 / 200 |
| Source groups | 170 |

**Status:** Preserved baseline. Do not modify.

**Output artefacts:** `results/` root directory.

---

### Experiment 2 — Template-Aware Combined-Group Evaluation

**Script:** `src/evaluate_experiment2.py`

**Grouping:** `groups = Combined_Group`

```
Combined_Group = Source_Group_ID relationships
              UNION detected shared-template relationships
```

Shared-template relationships are detected by 8-word shingle overlap (see `src/leakage.py`).
Transitive union-find merging is applied, so if A shares a source with B and B shares a template with C,
all three end up in the same Combined_Group.

This is a **leakage-sensitivity / robustness evaluation**.

It is **NOT** described as:
- a corrected true score
- a replacement for Experiment 1
- a more accurate generalization estimate
- a verified count of independent incidents

The difference between Experiment 1 (Macro-F1 ≈ 0.7402) and Experiment 2
comes from a stricter grouping assumption, not from a methodological error in Experiment 1.

#### Combined_Group framing

> Combined_Group count is a **heuristic leakage-control grouping count** used to make
> cross-validation more conservative. It is not a claim about the true independent-incident
> count in the underlying population.

| Grouping statistic | Value |
|-------------------|-------|
| Source groups (Experiment 1) | 170 |
| Template clusters detected | 125 |
| Combined groups (Experiment 2) | 109 |
| Largest combined group size | 26 |
| Combined groups with size > 1 | 14 |

All other settings are **identical** to Experiment 1: same dataset, same text field, same target,
same TF-IDF configuration, same Logistic Regression configuration, same random_state, same n_splits.

**Output artefacts:** `results/experiment_2_combined_group/` only. Never writes to `results/` root.

#### Cohort subgroup diagnostics

`src/cohort.py` assigns a `Dataset_Cohort` field based on `CV_Fold_5_GroupAware` nullity —
a historical collection-order marker only. This is **not** a validity, verification, or label-quality field.
`Gold_Label_Status` and `Verification_Status` are never consulted.

Cohort subgroup metrics are **OOF subgroup analysis** — predictions from the full 200-record
cross-validation filtered post-hoc by cohort. They are NOT two separate models.

---

## Evaluation vs. Full-Data Training

| Script | Purpose |
|--------|---------|
| `evaluate.py` | Experiment 1 — Source-Group-Aware Baseline (preserved) |
| `evaluate_experiment2.py` | Experiment 2 — Template-Aware Combined-Group Evaluation |
| `train.py` | Trains on ALL 200 records — produces the deployment artifact |

Run `evaluate.py` first to understand Experiment 1 performance.
Run `evaluate_experiment2.py` for the leakage-sensitivity evaluation.
Run `train.py` after to produce the final artifact.

---

## Directory Structure

```
ML/
├── data/
│   └── state_land_governance_confirmed_200.csv       # Authoritative dataset
│
├── src/
│   ├── config.py                  # Centralised configuration
│   ├── data_loader.py             # Validation and data loading
│   ├── model.py                   # Pipeline factory (TF-IDF + LR)
│   ├── evaluate.py                # Experiment 1 — Source-Group-Aware Baseline
│   ├── evaluate_experiment2.py    # Experiment 2 — Template-Aware Combined-Group
│   ├── leakage.py                 # Template-sharing detection (shingles + union-find)
│   ├── cohort.py                  # Historical dataset cohort labeling
│   ├── train.py                   # Full-dataset training
│   └── predict.py                 # Interactive CLI
│
├── models/
│   ├── governance_classifier.joblib   # Trained pipeline (generated, git-ignored)
│   └── model_metadata.json            # Training metadata (generated, git-ignored)
│
├── results/
│   ├── baseline_metrics.json          # Experiment 1 summary (generated, git-ignored)
│   ├── fold_metrics.csv               # Experiment 1 per-fold results
│   ├── per_class_metrics.csv          # Experiment 1 per-class P/R/F1/FPR
│   ├── confusion_matrix.csv           # Experiment 1 confusion matrix
│   ├── confusion_matrix.png           # Experiment 1 confusion matrix plot
│   ├── out_of_fold_predictions.csv    # Experiment 1 OOF predictions
│   ├── misclassified_cases.csv        # Experiment 1 incorrect predictions
│   └── experiment_2_combined_group/   # Experiment 2 outputs (all here, git-ignored)
│       ├── baseline_metrics.json
│       ├── fold_metrics.csv
│       ├── per_class_metrics.csv
│       ├── confusion_matrix.csv
│       ├── confusion_matrix.png
│       ├── out_of_fold_predictions.csv
│       ├── misclassified_cases.csv
│       └── template_cluster_audit.csv
│
├── tests/
│   ├── test_classifier.py         # Experiment 1 / model tests (33 tests)
│   ├── test_leakage.py            # leakage.py tests (35 tests)
│   └── test_cohort.py             # cohort.py tests (17 tests)
│
├── notebooks/
│   └── governance_classifier.ipynb    # Colab experiment reference
│
├── references/
│   ├── historical/                    # Historical reference scripts
│   ├── phase0/                        # Phase 0 data contract docs
│   └── experiment_2_patch_v2/         # Experiment 2 reference patch material
│
├── requirements.txt
└── README.md
```

---

## Installation

```bash
# From the ML/ directory
pip install -r requirements.txt
```

---

## Commands

All commands are run from the **`ML/`** directory.

### Run evaluation (group-aware cross-validation)

```bash
python src/evaluate.py
```

Outputs: `results/baseline_metrics.json`, `results/confusion_matrix.png`, and all other result artefacts.

### Train the final model (full dataset)

```bash
python src/train.py
```

Output: `models/governance_classifier.joblib`, `models/model_metadata.json`

### Interactive prediction

```bash
python src/predict.py
```

Type a complaint text at the prompt. Type `exit` to quit.

### Run Experiment 2 (source-and-text-grouped sensitivity evaluation)

```bash
python src/evaluate_experiment2.py
```

Outputs: `results/experiment2_source_text/` (never overwrites Experiment 1 baseline artefacts).

### Run automated tests

```bash
pytest
```

Or with verbose output:

```bash
pytest -v
```

Automated test suites:
- `tests/test_classifier.py` — model, data validation, and fit-predict tests (33)
- `tests/test_leakage_groups.py` — deterministic source-and-text grouping tests (17)

---

## Generated Outputs

| File | Contents |
|------|---------|
| `results/baseline_metrics.json` | Full evaluation summary, config, fold results, overall metrics, per-class metrics, confusion matrix, dummy baseline |
| `results/fold_metrics.csv` | Accuracy and F1 for each of the 5 folds |
| `results/per_class_metrics.csv` | TP, FP, FN, TN, Precision, Recall, F1, FPR per class |
| `results/confusion_matrix.csv` | Raw confusion matrix (rows=true, cols=predicted) |
| `results/confusion_matrix.png` | Colour-mapped confusion matrix plot |
| `results/out_of_fold_predictions.csv` | Research_ID, text, true label, predicted label, fold, probabilities |
| `results/misclassified_cases.csv` | Subset of OOF predictions where True ≠ Predicted |
| `models/governance_classifier.joblib` | Trained sklearn Pipeline |
| `models/model_metadata.json` | Training provenance and configuration |

---

## Limitations

- **Dataset size**: 200 records. Performance estimates from cross-validation are indicative; the dataset is small for a text classifier.
- **Text only**: No geographic, institutional, or other metadata is used. Future phases may incorporate additional feature types.
- **No probability calibration**: `predict_proba` output is raw logistic-regression probability — not calibrated. Do not interpret it as a precise confidence interval.
- **Proposed labels**: All ML labels are proposed research labels. They have not been independently reviewed by a domain expert for each record. Label quality affects classifier quality.
- **Fixed baseline architecture**: TF-IDF + Logistic Regression is the approved baseline. Hyperparameter tuning, character n-grams, and transformer-based models are explicitly deferred to later phases.
- **Advisory output only**: All classifier outputs are classification intelligence, not legal determinations.

---

## Research Integrity Note

Record verification ≠ independent ML-label review.

Some records were confirmed as genuine incidents by the Land Commissioner.
Incident existence confirmation does not automatically confirm the proposed ML label.
All labels carry the `Gold_Label_Status = "Proposed research label — final human/domain review required"`.

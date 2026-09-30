# Colombo OSM backend-compatible candidate

## Feature schema (exact predictors)

Numeric: area_hectares, distance_to_road_m, distance_to_water_m

Categorical: requested_purpose, land_category, gis_enrichment_status, derived_soil_group, spatial_constraint_present

Excluded vs prior `output_colombo_osm`: environmental_restriction_type, environmental_restriction_severity.

## Missing-evidence / abstention policy

Adapter leaves null when conservation assessment is unavailable. Never coerce null/Unknown to False. Backend-compatible serving must abstain from experimental prediction when SpatialConstraintPresent is null.

OneHotEncoder(handle_unknown='ignore') is an engineering fallback only; it is not evidence of predictive validity for unseen categories.

## Selection (training-only CV)

Selected by mean CV macro-F1: **Random Forest**

{
  "folds": 5,
  "models": {
    "Dummy Majority": {
      "cv_accuracy_mean": 0.4582368685178989,
      "cv_accuracy_std": 0.02627509847047631,
      "cv_macro_f1_mean": 0.20934488077112948,
      "cv_macro_f1_std": 0.008252379300102414,
      "fold_macro_f1": [
        0.2104283054003724,
        0.2191780821917808,
        0.2005730659025788,
        0.21731123388581952,
        0.1992337164750958
      ],
      "fold_accuracy": [
        0.46122448979591835,
        0.4897959183673469,
        0.430327868852459,
        0.48360655737704916,
        0.4262295081967213
      ],
      "labels_in_training_pool": [
        "Unsuitable",
        "Moderately Suitable",
        "Suitable"
      ]
    },
    "Logistic Regression": {
      "cv_accuracy_mean": 0.6113348946135831,
      "cv_accuracy_std": 0.026895533445243786,
      "cv_macro_f1_mean": 0.5715285954349887,
      "cv_macro_f1_std": 0.0325939381985375,
      "fold_macro_f1": [
        0.5375322246373148,
        0.5291013103259327,
        0.5805414392370915,
        0.6063864205969468,
        0.6040815823776581
      ],
      "fold_accuracy": [
        0.5755102040816327,
        0.5959183673469388,
        0.6024590163934426,
        0.6516393442622951,
        0.6311475409836066
      ],
      "labels_in_training_pool": [
        "Unsuitable",
        "Moderately Suitable",
        "Suitable"
      ]
    },
    "Random Forest": {
      "cv_accuracy_mean": 0.6489595182335229,
      "cv_accuracy_std": 0.013904202318530522,
      "cv_macro_f1_mean": 0.6562758676097806,
      "cv_macro_f1_std": 0.022361114802456273,
      "fold_macro_f1": [
        0.6376108530570298,
        0.6287788481811253,
        0.6930286546710341,
        0.660435463659148,
        0.6615255184805662
      ],
      "fold_accuracy": [
        0.6326530612244898,
        0.636734693877551,
        0.6721311475409836,
        0.6516393442622951,
        0.6516393442622951
      ],
      "labels_in_training_pool": [
        "Unsuitable",
        "Moderately Suitable",
        "Suitable"
      ]
    }
  },
  "selected_by_cv_macro_f1": "Random Forest"
}

## Exploratory held-out (not for model selection)

Macro-F1=0.5940, accuracy=0.6079

This held-out set already informed development (sensitivity analysis). These numbers are exploratory.

## Limitations

suitability_label remains rule-simulated and may have used environmental restriction signals that are no longer predictors. Never retarget labels to inflate scores. Real-world validity requires independent evaluation and real suitability labels.

Real-world validity requires an independent evaluation set and real suitability labels that were not used during development.

ModelActivationEnabled remains false. `ml_service.py` and live `output/` were not modified.

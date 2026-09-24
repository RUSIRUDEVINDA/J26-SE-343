# Colombo OSM GIS-grounded offline evaluation

Generated: 2026-09-20T15:16:31.608987+00:00
Dataset: `C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance\data\gis\experiments\colombo\parcels_colombo_osm_v2.csv` (1500 rows)

## What this measures

Agreement with **simulated** labeling rules on GIS-grounded features for
sampled Colombo locations — **not** real suitability ground truth.

## Missing-data decisions

- Excluded `elevation_meters`, `soil_overlap_percentage` (all missing).
- Excluded `terrain_description` (constant Unknown).
- Excluded constant `province` / `district` for this district-only set.
- sklearn SimpleImputer(median) on all-NaN columns **skips** them (sklearn 1.9+);
  explicit exclusion records the decision instead of relying on skip behaviour.

## Spatial hold-out

- GroupShuffleSplit on lat/lon grid cells (cell=0.02°, seed=42)
- Train n=1222 / Test n=278
- Train labels: {'Suitable': 560, 'Moderately Suitable': 509, 'Unsuitable': 153}
- Test labels: {'Moderately Suitable': 126, 'Suitable': 121, 'Unsuitable': 31}
- Classes missing in test: none

## Model comparison (held-out)

| Model | Accuracy | Macro-F1 |
|---|---:|---:|
| Dummy Majority | 0.4353 | 0.2022 |
| Logistic Regression | 0.7302 | 0.7065 |
| Random Forest | 0.7842 | 0.7826 |

## Production status

- Artifacts are under this experiment directory only.
- Live `output/random_forest_pipeline.joblib` and `ml_service.py` unchanged.
- Not compatible with expressway-based live enrichment merely by feature name.


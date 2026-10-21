# Standard ML evaluation report

Synthetic labels from trustworthy parcel fields only. Road, water, and soil were **not** invented or used.

- Held-out Random Forest accuracy: **0.7650** (76.50%)
- Dummy majority baseline accuracy: **0.4950**
- 80% target achieved: **no**

## Why 80% is not claimed

Credible predictors without road/water/soil GIS leave substantial overlap between classes once label noise is kept honest. Reaching 80% would require either inventing unavailable GIS fields, reducing label noise to chase the target, or real expert-labelled parcels with measured enrichment. Until then, keep ML **supplementary** and do not present the target as achieved.

### Data needed for a credible ≥80% claim

- Expert or historically validated suitability labels (not rule-replay)
- Measured GIS road/water/soil only when enrichment actually exists
- Enough labelled rows per class for a stable held-out set

## Per-class (Random Forest, held-out)

| Class | Precision | Recall | F1 | Support |
|-------|-----------|--------|----|---------|
| Unsuitable | 0.8172 | 0.8172 | 0.8172 | 279 |
| Moderately Suitable | 0.7687 | 0.7609 | 0.7648 | 297 |
| Suitable | 0.1852 | 0.2083 | 0.1961 | 24 |

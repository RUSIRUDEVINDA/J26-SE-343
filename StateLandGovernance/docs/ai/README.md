# AI / ML (Component 1)

Land Intelligence uses a supplementary Random Forest suitability model. The model does **not** override rule-based recommendation scoring or hard constraints.

**Standard service (port 8500):** held-out accuracy is below the 80% research target (~76.5% after excluding invented road/water/soil). Keep ML supplementary; do not present 80% as achieved. See metrics in `src/Modules/LandIntelligence/ML/output/`.

**Colombo experimental GIS ML** is a separate opt-in path (port 8501) and must not replace the standard `output/` artifacts.

See [src/Modules/LandIntelligence/ML/README.md](../../src/Modules/LandIntelligence/ML/README.md) for training, serving, and `.env` configuration (`ML_SUITABILITY_SERVICE_URL`).

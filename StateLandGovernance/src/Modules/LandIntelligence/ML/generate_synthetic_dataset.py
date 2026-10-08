"""
Synthetic parcel-suitability dataset generator
------------------------------------------------
Component 1 - Intelligent Land Knowledge Graph and Spatial Recommendation Platform
J26-SE-343

Generates a labelled dataset that mirrors the fields actually produced by your
.NET domain model (LandParcel, AdministrativeLocation, LandCharacteristics,
ParcelGisDerivedIntelligence, SpatialConstraint, EnvironmentalRestriction) plus
an applicant-requested purpose, so the ML experiment lines up with Table 2 of
the proposal ("Data / Participants / Experimental Inputs").

USE THIS ONLY until real, expert-labelled or validated-historical data is
available (per section 3.5 Validation Strategy). Replace this script's output
with a real export the moment you have one — the report should say clearly
whether results are from synthetic or real data.

Run:
    python generate_synthetic_dataset.py --n 3000 --out parcels_dataset.csv
"""

import argparse
import numpy as np
import pandas as pd

PROVINCES_DISTRICTS = {
    "Western": ["Colombo", "Gampaha", "Kalutara"],
    "Central": ["Kandy", "Matale", "Nuwara Eliya"],
    "Southern": ["Galle", "Matara", "Hambantota"],
    "North Western": ["Kurunegala", "Puttalam"],
    "Sabaragamuwa": ["Ratnapura", "Kegalle"],
}

LAND_CATEGORY = ["StateLand", "CrownLand", "ReservedLand", "Other"]
SOIL_GROUPS = ["Reddish Brown Earth", "Alluvial", "Lateritic", "Regosol", "Bog and Half-Bog", "Unknown"]
TERRAIN = ["Flat", "Undulating", "Hilly", "Steep", "Unknown"]
ENV_RESTRICTION = ["None", "ProtectedArea", "ForestReserve", "WaterBodyBuffer", "Wetland", "WildlifeCorridor"]
RESTRICTION_SEVERITY = {"None": "None", "ProtectedArea": "High", "ForestReserve": "High",
                         "WaterBodyBuffer": "Medium", "Wetland": "Medium", "WildlifeCorridor": "Prohibitive"}
PURPOSES = ["Agricultural", "Residential", "Commercial"]
GIS_STATUS = ["Complete", "Partial", "Unavailable"]

LABELS = ["Unsuitable", "Moderately Suitable", "Suitable"]


def sample_parcels(n: int, rng: np.random.Generator) -> pd.DataFrame:
    provinces = rng.choice(list(PROVINCES_DISTRICTS.keys()), size=n)
    districts = [rng.choice(PROVINCES_DISTRICTS[p]) for p in provinces]

    df = pd.DataFrame({
        "parcel_id": [f"P{100000 + i}" for i in range(n)],
        "requested_purpose": rng.choice(PURPOSES, size=n),
        "land_category": rng.choice(LAND_CATEGORY, size=n, p=[0.55, 0.15, 0.2, 0.1]),
        "area_hectares": np.round(rng.lognormal(mean=0.4, sigma=0.9, size=n).clip(0.05, 200), 3),
        "province": provinces,
        "district": districts,
        "elevation_meters": np.round(rng.normal(250, 220, size=n).clip(0, 2200), 1),
        "distance_to_road_m": np.round(rng.exponential(scale=600, size=n).clip(5, 15000), 1),
        "distance_to_water_m": np.round(rng.exponential(scale=900, size=n).clip(5, 20000), 1),
        "gis_enrichment_status": rng.choice(GIS_STATUS, size=n, p=[0.65, 0.25, 0.10]),
    })

    # Soil / terrain: sometimes missing ("Unknown"), matching "missing evidence
    # stays Unknown/Unavailable" rule from the proposal rather than being imputed
    # into a favourable value.
    soil = rng.choice(SOIL_GROUPS[:-1], size=n)
    terrain = rng.choice(TERRAIN[:-1], size=n)
    missing_soil = rng.random(n) < 0.12
    missing_terrain = rng.random(n) < 0.10
    df["derived_soil_group"] = np.where(missing_soil, "Unknown", soil)
    df["soil_overlap_percentage"] = np.where(
        missing_soil, np.nan, np.round(rng.uniform(30, 100, size=n), 1)
    )
    df["terrain_description"] = np.where(missing_terrain, "Unknown", terrain)

    # Environmental restriction: mostly None, occasionally a real constraint
    env = rng.choice(ENV_RESTRICTION, size=n, p=[0.72, 0.07, 0.06, 0.06, 0.05, 0.04])
    df["environmental_restriction_type"] = env
    df["environmental_restriction_severity"] = [RESTRICTION_SEVERITY[e] for e in env]

    # Spatial constraint flag (zoning / buffer / setback) independent-ish of env restriction
    df["spatial_constraint_present"] = rng.random(n) < 0.18

    # Force GIS-unavailable rows to blank out derived fields, as the real
    # pipeline would (Unknown/Unavailable rather than a fabricated value).
    unavailable = df["gis_enrichment_status"] == "Unavailable"
    df.loc[unavailable, ["distance_to_road_m", "distance_to_water_m", "derived_soil_group",
                          "soil_overlap_percentage", "terrain_description"]] = np.nan
    df.loc[unavailable, "derived_soil_group"] = "Unknown"
    df.loc[unavailable, "terrain_description"] = "Unknown"

    return df


def rule_based_label(row: pd.Series, rng: np.random.Generator) -> str:
    """
    Deterministic-ish ground truth used only to generate a plausible synthetic
    target. Mirrors the *kind* of hard-constraint-first logic your rule engine
    uses (section 3.3/3.5): a Prohibitive/High restriction blocks Suitable
    regardless of score, missing evidence never resolves to a favourable value.
    """
    score = 0.0
    purpose = row["requested_purpose"]

    # Hard constraint: prohibitive / high-severity restriction caps suitability
    severity = row["environmental_restriction_severity"]
    hard_block = severity in ("Prohibitive", "High")

    # Road accessibility (matters more for Commercial/Residential)
    road = row["distance_to_road_m"]
    if pd.notna(road):
        if purpose in ("Commercial", "Residential"):
            score += 2.0 if road < 300 else (1.0 if road < 1000 else -1.0)
        else:
            score += 1.0 if road < 1500 else 0.0

    # Water proximity (matters more for Agricultural)
    water = row["distance_to_water_m"]
    if pd.notna(water):
        if purpose == "Agricultural":
            score += 2.0 if water < 500 else (1.0 if water < 1500 else -0.5)
        else:
            score += 0.5 if water < 2000 else 0.0

    # Soil (Agricultural only, unknown soil is neutral, not favourable)
    soil = row["derived_soil_group"]
    if purpose == "Agricultural" and soil not in ("Unknown", None):
        score += 1.5 if soil in ("Reddish Brown Earth", "Alluvial") else 0.0

    # Area adequacy heuristic
    area = row["area_hectares"]
    min_area = {"Agricultural": 1.0, "Residential": 0.05, "Commercial": 0.1}[purpose]
    score += 1.0 if area >= min_area else -1.5

    # Spatial constraint (zoning/setback) is a soft negative, not a hard block
    if row["spatial_constraint_present"]:
        score -= 1.0

    # GIS data completeness: partial/unavailable data pulls toward the
    # cautious middle class rather than "Suitable"
    if row["gis_enrichment_status"] == "Partial":
        score -= 0.5
    elif row["gis_enrichment_status"] == "Unavailable":
        score -= 1.5

    # Add noise so the problem isn't trivially separable for the model
    score += rng.normal(0, 0.9)

    if hard_block:
        return "Unsuitable" if score < 1.5 else "Moderately Suitable"
    if score >= 2.5:
        return "Suitable"
    if score >= 0.0:
        return "Moderately Suitable"
    return "Unsuitable"


def main():
    parser = argparse.ArgumentParser(description="Generate synthetic parcel-suitability dataset")
    parser.add_argument("--n", type=int, default=3000, help="number of rows")
    parser.add_argument("--out", type=str, default="parcels_dataset.csv")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    rng = np.random.default_rng(args.seed)
    df = sample_parcels(args.n, rng)
    df["suitability_label"] = df.apply(lambda r: rule_based_label(r, rng), axis=1)

    df.to_csv(args.out, index=False)
    print(f"Wrote {len(df)} rows to {args.out}")
    print("\nLabel distribution:")
    print(df["suitability_label"].value_counts())


if __name__ == "__main__":
    main()

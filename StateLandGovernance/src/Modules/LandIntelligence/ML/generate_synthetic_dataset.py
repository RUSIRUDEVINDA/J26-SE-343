"""
Synthetic parcel-suitability dataset generator
------------------------------------------------
Component 1 - Intelligent Land Knowledge Graph and Spatial Recommendation Platform
J26-SE-343

Generates a labelled dataset from parcel fields that are trustworthy for the
standard (non-Colombo) ML path: cadastre/admin attributes, applicant purpose,
environmental restrictions, and spatial-constraint flags.

Road, water, and soil measurements are NOT invented and are NOT used for
labelling. Columns may appear as null / "Unknown" so callers see that GIS
evidence is unavailable — the same posture as the Colombo research import
without enrichment snapshots.

USE THIS ONLY until real, expert-labelled or validated-historical data is
available. Replace this script's output with a real export when you have one —
reports must say clearly whether results are from synthetic or real data.

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
TERRAIN = ["Flat", "Undulating", "Hilly", "Steep", "Unknown"]
ENV_RESTRICTION = [
    "None",
    "ProtectedArea",
    "ForestReserve",
    "WaterBodyBuffer",
    "Wetland",
    "WildlifeCorridor",
]
RESTRICTION_SEVERITY = {
    "None": "None",
    "ProtectedArea": "High",
    "ForestReserve": "High",
    "WaterBodyBuffer": "Medium",
    "Wetland": "Medium",
    "WildlifeCorridor": "Prohibitive",
}
PURPOSES = ["Agricultural", "Residential", "Commercial"]
GIS_STATUS = ["Complete", "Partial", "Unavailable"]

LABELS = ["Unsuitable", "Moderately Suitable", "Suitable"]


def sample_parcels(n: int, rng: np.random.Generator) -> pd.DataFrame:
    provinces = rng.choice(list(PROVINCES_DISTRICTS.keys()), size=n)
    districts = [rng.choice(PROVINCES_DISTRICTS[p]) for p in provinces]

    # Research-aligned GIS posture: most rows lack enrichment (Unavailable/Partial).
    df = pd.DataFrame({
        "parcel_id": [f"P{100000 + i}" for i in range(n)],
        "requested_purpose": rng.choice(PURPOSES, size=n),
        "land_category": rng.choice(LAND_CATEGORY, size=n, p=[0.55, 0.15, 0.2, 0.1]),
        "area_hectares": np.round(rng.lognormal(mean=0.4, sigma=0.9, size=n).clip(0.05, 200), 3),
        "province": provinces,
        "district": districts,
        "elevation_meters": np.round(rng.normal(250, 220, size=n).clip(0, 2200), 1),
        "gis_enrichment_status": rng.choice(GIS_STATUS, size=n, p=[0.20, 0.30, 0.50]),
    })

    # Road / water / soil: never invent confirmed measurements.
    df["distance_to_road_m"] = np.nan
    df["distance_to_water_m"] = np.nan
    df["derived_soil_group"] = "Unknown"
    df["soil_overlap_percentage"] = np.nan

    terrain = rng.choice(TERRAIN[:-1], size=n)
    missing_terrain = rng.random(n) < 0.35
    df["terrain_description"] = np.where(missing_terrain, "Unknown", terrain)

    env = rng.choice(ENV_RESTRICTION, size=n, p=[0.72, 0.07, 0.06, 0.06, 0.05, 0.04])
    df["environmental_restriction_type"] = env
    df["environmental_restriction_severity"] = [RESTRICTION_SEVERITY[e] for e in env]

    df["spatial_constraint_present"] = rng.random(n) < 0.18

    # When GIS is Unavailable, blank optional physical attributes (elevation/terrain).
    unavailable = df["gis_enrichment_status"] == "Unavailable"
    df.loc[unavailable, "elevation_meters"] = np.nan
    df.loc[unavailable, "terrain_description"] = "Unknown"

    return df


def rule_based_label(row: pd.Series, rng: np.random.Generator) -> str:
    """
    Synthetic target from trustworthy fields only.

    Does not use road, water, or soil. Hard restrictions block Suitable.
    Missing GIS evidence never resolves to a favourable boost.
    """
    score = 0.0
    purpose = row["requested_purpose"]
    severity = row["environmental_restriction_severity"]
    hard_block = severity in ("Prohibitive", "High")

    area = row["area_hectares"]
    min_area = {"Agricultural": 1.0, "Residential": 0.05, "Commercial": 0.1}[purpose]
    score += 1.5 if area >= min_area else -1.5

    category = row["land_category"]
    if category == "ReservedLand":
        score -= 1.5
    elif category == "StateLand":
        score += 0.5
    elif category == "CrownLand":
        score += 0.25

    if row["spatial_constraint_present"]:
        score -= 1.0

    # Medium severity soft penalty (High/Prohibitive handled as hard_block).
    if severity == "Medium":
        score -= 0.75

    if row["gis_enrichment_status"] == "Partial":
        score -= 0.5
    elif row["gis_enrichment_status"] == "Unavailable":
        score -= 1.0

    elev = row["elevation_meters"]
    if pd.notna(elev) and purpose == "Agricultural" and elev > 1200:
        score -= 0.5

    terrain = row["terrain_description"]
    if purpose in ("Residential", "Commercial") and terrain == "Steep":
        score -= 0.5

    # Keep realistic noise — do not reduce noise to chase an accuracy target.
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
    print("\nRoad/water/soil: all unavailable (not invented).")
    print(
        f"distance_to_road_m non-null: {df['distance_to_road_m'].notna().sum()}; "
        f"soil groups non-Unknown: {(df['derived_soil_group'] != 'Unknown').sum()}"
    )


if __name__ == "__main__":
    main()

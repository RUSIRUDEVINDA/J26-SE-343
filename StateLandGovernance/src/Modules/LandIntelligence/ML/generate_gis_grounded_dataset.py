"""
Generate a Colombo GIS-grounded suitability dataset (offline experiment).

Uses prepared nationwide OSM motor-road linework for distance_to_road_m and
available LandIntelligence_GIS soil/water layers. Sample locations are random
points inside the Colombo district boundary — not verified cadastral parcels.

Does not retrain models, modify ml_service.py, or import into PostgreSQL.

Usage (from StateLandGovernance/ solution folder):
    py src/Modules/LandIntelligence/ML/generate_gis_grounded_dataset.py ^
        --n 1500 --district Colombo --seed 42 ^
        --out data/gis/experiments/colombo/parcels_colombo_osm.csv
"""

from __future__ import annotations

import argparse
import sys
from datetime import datetime, timezone
from pathlib import Path

import geopandas as gpd
import numpy as np
import pandas as pd

_TOOLS_DIR = Path(__file__).resolve().parent / "tools"
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from gis_grounded_dataset_helpers import (  # noqa: E402
    AUDIT_COLUMNS,
    LOCAL_PROJECTED_CRS,
    TRAINING_CATEGORICAL,
    TRAINING_COLUMNS,
    TRAINING_NUMERIC,
    TRAINING_TARGET,
    WGS84,
    file_sha256,
    load_district_boundary,
    load_water_features,
    nearest_features_projected,
    point_in_conservation,
    point_soil_group,
    rule_based_simulated_label,
    sample_points_in_polygon,
    validate_projected_vs_geodesic,
    verify_motor_roads_gpkg,
    write_json,
)
from osm_motor_road_policy import (  # noqa: E402
    POLICY_VERSION,
    PROXIMITY_DISCLAIMER,
)
from prepare_osm_motor_roads import resolve_repository_root  # noqa: E402

LABEL_RULE_VERSION = "simulated-v1-synthetic-spirit-2026-03-20"
DEFAULT_DISTRICT = "Colombo"
DEFAULT_N = 1500
DEFAULT_SEED = 42

LAND_CATEGORY = ["StateLand", "CrownLand", "ReservedLand", "Other"]
PURPOSES = ["Agricultural", "Residential", "Commercial"]
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

SIMULATED_FIELDS = [
    "requested_purpose",
    "land_category",
    "area_hectares",
    "environmental_restriction_type",
    "environmental_restriction_severity",
    "suitability_label",
]


def default_paths(root: Path) -> dict[str, Path]:
    gis = root / "data" / "gis" / "LandIntelligence_GIS"
    exp = root / "data" / "gis" / "experiments" / "colombo"
    return {
        "roads_gpkg": exp / "osm_motor_roads.gpkg",
        "roads_manifest": exp / "osm_motor_roads_manifest.json",
        "districts": gis / "boundaries" / "district_boundaries.geojson",
        "soil": gis / "soil" / "soil_groups.geojson",
        "soil_conservation": gis / "soil" / "soil_conservation_areas.geojson",
        "canals": gis / "water" / "canals.geojson",
        "lakes": gis / "water" / "lakes.geojson",
        "out_csv": exp / "parcels_colombo_osm.csv",
        "out_manifest": exp / "parcels_colombo_osm_manifest.json",
    }


def enrichment_status(row: pd.Series) -> str:
    """
    Status of *assessable* GIS evidence for point samples.

    Assessable here: road proximity, water proximity, soil-group polygon.
    Elevation, terrain, and soil_overlap_percentage are structurally
    unavailable for point samples and never contribute to AssessedComplete.
    """
    road_ok = pd.notna(row["distance_to_road_m"])
    water_ok = pd.notna(row["distance_to_water_m"])
    soil_ok = row["derived_soil_group"] not in ("Unknown", None) and pd.notna(
        row["derived_soil_group"]
    )
    if not road_ok:
        return "Unavailable"
    if road_ok and water_ok and soil_ok:
        return "AssessedComplete"
    return "AssessedPartial"


def generate_dataset(
    n: int,
    district: str,
    seed: int,
    out_csv: Path,
    out_manifest: Path,
    paths: dict[str, Path],
) -> dict:
    if out_csv.exists():
        raise FileExistsError(
            f"Refusing to overwrite existing dataset: {out_csv}. "
            "Choose a new --out path."
        )
    if out_manifest.exists():
        raise FileExistsError(
            f"Refusing to overwrite existing manifest: {out_manifest}."
        )

    road_check = verify_motor_roads_gpkg(paths["roads_gpkg"], layer="osm_motor_roads")
    boundary, province, district_canonical = load_district_boundary(
        paths["districts"], district
    )

    roads_manifest = {}
    if paths["roads_manifest"].is_file():
        import json

        roads_manifest = json.loads(paths["roads_manifest"].read_text(encoding="utf-8"))
        policy = roads_manifest.get("policy_version")
        if policy and policy != POLICY_VERSION:
            # Report discrepancy; do not silently change filter policy.
            print(
                f"WARNING: roads manifest policy_version={policy!r} differs from "
                f"code POLICY_VERSION={POLICY_VERSION!r}. Dataset will cite the "
                "manifest value and will not alter the road filter.",
                file=sys.stderr,
            )

    rng = np.random.default_rng(seed)
    points = sample_points_in_polygon(boundary, n, rng)
    points_gdf = gpd.GeoDataFrame(
        {"parcel_id": [f"COL-OSM-{seed}-{i:06d}" for i in range(n)]},
        geometry=points,
        crs=WGS84,
    )

    # --- GIS: motor-road proximity (nationwide, not clipped to district) ---
    roads = gpd.read_file(paths["roads_gpkg"], layer="osm_motor_roads")
    road_cols = ["osm_id", "highway", "adm2_name"]
    road_join = nearest_features_projected(
        points_gdf,
        roads,
        value_columns=road_cols,
        distance_column="distance_to_road_m",
    )
    # Map joined feature index for geodesic validation (sjoin stores index_right).
    feature_idx = road_join.get("index_right")
    if feature_idx is None:
        # GeoPandas may name the right index differently; recover via osm_id match.
        osm_to_idx = pd.Series(roads.index, index=roads["osm_id"].astype(str))
        feature_idx = road_join["osm_id"].astype(str).map(osm_to_idx)

    road_validation = validate_projected_vs_geodesic(
        points_gdf,
        roads,
        projected_distances=road_join["distance_to_road_m"],
        feature_indices=feature_idx,
        sample_size=8,
        max_abs_error_m=5.0,
        rng=np.random.default_rng(seed + 7),
    )
    if not road_validation["passed"]:
        raise RuntimeError(
            "Projected vs geodesic road-distance validation failed: "
            f"{road_validation}"
        )

    # Outside-district nearest-road check: at least one sample should have
    # nearest road adm2 != district when network extends beyond the boundary.
    outside_district_hits = int(
        (road_join["adm2_name"].astype(str).str.casefold() != district_canonical.casefold())
        .fillna(False)
        .sum()
    )

    # --- GIS: water proximity (nationwide canals + lakes; no district clip) ---
    water = load_water_features(paths["canals"], paths["lakes"])
    water_join = nearest_features_projected(
        points_gdf,
        water,
        value_columns=["water_feature_source", "water_feature_name"],
        distance_column="distance_to_water_m",
    )

    # --- GIS: soil group under point; overlap % intentionally unavailable ---
    soil_series = point_soil_group(points_gdf, paths["soil"])
    conservation_hits = point_in_conservation(points_gdf, paths["soil_conservation"])

    # --- Simulated applicant / parcel attributes (not real cadastral data) ---
    env = rng.choice(ENV_RESTRICTION, size=n, p=[0.72, 0.07, 0.06, 0.06, 0.05, 0.04])

    df = pd.DataFrame(
        {
            "parcel_id": points_gdf["parcel_id"],
            "sample_kind": "sampled_location_not_cadastral_parcel",
            "latitude": [pt.y for pt in points],
            "longitude": [pt.x for pt in points],
            "requested_purpose": rng.choice(PURPOSES, size=n),
            "land_category": rng.choice(
                LAND_CATEGORY, size=n, p=[0.55, 0.15, 0.2, 0.1]
            ),
            "area_hectares": np.round(
                rng.lognormal(mean=0.4, sigma=0.9, size=n).clip(0.05, 200), 3
            ),
            "province": province,
            "district": district_canonical,
            "elevation_meters": np.nan,  # no DEM in LandIntelligence_GIS
            "distance_to_road_m": np.round(road_join["distance_to_road_m"].astype(float), 3),
            "distance_to_water_m": np.round(
                water_join["distance_to_water_m"].astype(float), 3
            ),
            "derived_soil_group": soil_series.values,
            "soil_overlap_percentage": np.nan,  # cannot infer from a sample point
            "terrain_description": "Unknown",  # no terrain layer for Colombo
            "environmental_restriction_type": env,
            "environmental_restriction_severity": [RESTRICTION_SEVERITY[e] for e in env],
            "spatial_constraint_present": conservation_hits.values.astype(bool),
            "nearest_road_osm_id": road_join["osm_id"].astype(str).values,
            "nearest_road_highway": road_join["highway"].astype(str).values,
            "nearest_road_adm2_name": road_join["adm2_name"].astype(str).values,
            "road_distance_method": "sjoin_nearest_EPSG32644_metres_validated_vs_geodesic",
            "road_projected_crs": LOCAL_PROJECTED_CRS,
            "water_feature_source": water_join["water_feature_source"].astype(str).values,
            "soil_source_layer": "soil_groups",
            "spatial_constraint_source": "soil_conservation_areas_intersection",
            "label_rule_version": LABEL_RULE_VERSION,
            "simulated_fields": ";".join(SIMULATED_FIELDS),
        }
    )

    df["gis_enrichment_status"] = df.apply(enrichment_status, axis=1)
    # Labels: simulated only; seed continues from same Generator for reproducibility.
    df[TRAINING_TARGET] = [
        rule_based_simulated_label(row, rng) for _, row in df.iterrows()
    ]

    # Containment check (handles holes via contains on original boundary).
    outside = [
        i
        for i, pt in enumerate(points)
        if not boundary.contains(pt)
    ]
    if outside:
        raise RuntimeError(f"{len(outside)} samples fall outside the district boundary")

    # Distance sanity
    for col in ("distance_to_road_m", "distance_to_water_m"):
        series = df[col].dropna()
        if len(series) and (series < 0).any():
            raise RuntimeError(f"{col} contains negative values")
        if len(series) and (~np.isfinite(series)).any():
            raise RuntimeError(f"{col} contains non-finite values")

    out_csv.parent.mkdir(parents=True, exist_ok=True)
    # Column order: training contract first, then audit metadata.
    ordered = [c for c in TRAINING_COLUMNS if c in df.columns]
    ordered += [c for c in AUDIT_COLUMNS if c in df.columns and c not in ordered]
    ordered += [c for c in df.columns if c not in ordered]
    df[ordered].to_csv(out_csv, index=False)

    missing = {
        col: int(df[col].isna().sum()) if col in df.columns else n
        for col in TRAINING_NUMERIC + TRAINING_CATEGORICAL
        if col in df.columns
    }
    # Count Unknown categoricals as missing evidence where applicable
    missing_evidence = {
        "elevation_meters_na": int(df["elevation_meters"].isna().sum()),
        "soil_overlap_percentage_na": int(df["soil_overlap_percentage"].isna().sum()),
        "distance_to_road_m_na": int(df["distance_to_road_m"].isna().sum()),
        "distance_to_water_m_na": int(df["distance_to_water_m"].isna().sum()),
        "derived_soil_group_unknown": int((df["derived_soil_group"] == "Unknown").sum()),
        "terrain_description_unknown": int((df["terrain_description"] == "Unknown").sum()),
    }

    input_checksums = {}
    for key in (
        "roads_gpkg",
        "districts",
        "soil",
        "soil_conservation",
        "canals",
        "lakes",
        "roads_manifest",
    ):
        path = paths[key]
        if path.is_file():
            input_checksums[key] = {
                "path": str(path.resolve()),
                "sha256": file_sha256(path),
            }

    road_source = roads_manifest.get("source", {})
    manifest = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "dataset_kind": (
            "real GIS-derived features with simulated attributes/labels; "
            "not observed real-world suitability ground truth"
        ),
        "sample_kind": "sampled_locations_inside_district_boundary_not_cadastral_parcels",
        "seed": seed,
        "sample_count": n,
        "district": district_canonical,
        "province": province,
        "output": {
            "csv": str(out_csv.resolve()),
            "row_count": len(df),
            "sha256": file_sha256(out_csv),
        },
        "inputs": input_checksums,
        "boundary": {
            "path": str(paths["districts"].resolve()),
            "layer_or_file": "district_boundaries.geojson",
            "crs_epsg": 4326,
            "district_name": district_canonical,
            "sampling": "uniform rejection sampling with polygon.contains (holes/multipart safe)",
        },
        "road_network": {
            "path": str(paths["roads_gpkg"].resolve()),
            "layer": "osm_motor_roads",
            "feature_count": road_check["feature_count"],
            "crs_epsg": 4326,
            "spatial_index_verified": True,
            "filter_policy_version": roads_manifest.get("policy_version", POLICY_VERSION),
            "code_policy_version": POLICY_VERSION,
            "osm_snapshot": road_source.get("snapshot"),
            "license": road_source.get("license"),
            "license_url": road_source.get("license_url"),
            "proximity_disclaimer": PROXIMITY_DISCLAIMER,
            "distance_to_road_m_definition": (
                "Metres from sample point to nearest mapped motor-road centreline "
                "in the filtered OSM allowlist network. Not verified public access, "
                "not travel-network distance, not distance to a motorway entrance."
            ),
            "distance_implementation": {
                "method": "geopandas.sjoin_nearest on projected coordinates",
                "projected_crs": LOCAL_PROJECTED_CRS,
                "units": "metres",
                "roads_clipped_to_district": False,
                "geodesic_validation": road_validation,
            },
            "nearest_road_outside_district_sample_count": outside_district_hits,
        },
        "water": {
            "sources": ["canals.geojson", "lakes.geojson"],
            "clipped_to_district": False,
            "note": (
                "Canals layer has no features intersecting Colombo; lakes provide "
                "local water features. Distance uses the combined nationwide layers."
            ),
            "features_loaded": len(water),
        },
        "soil": {
            "layer": "soil_groups.geojson",
            "derived_soil_group": "name of soil polygon containing the sample point",
            "soil_overlap_percentage": (
                "always null — parcel polygon overlap cannot be inferred from a point"
            ),
        },
        "spatial_constraint": {
            "source": "soil_conservation_areas.geojson intersection",
            "true_means": "sample intersects an imported soil conservation polygon (mapped intersection)",
            "false_means": (
                "no intersection with the imported conservation layer "
                "(verified absence of mapped intersection within assessed coverage); "
                "not a claim that all real-world spatial constraints are absent; "
                "not Unknown"
            ),
            "note": (
                "Boolean intersection only. Legal environmental restrictions are "
                "NOT inferred from soil-group names."
            ),
        },
        "gis_enrichment_status_semantics": {
            "AssessedComplete": (
                "Road, water, and soil-group evidence are available for this point. "
                "Does NOT mean elevation, terrain, or soil_overlap_percentage are present "
                "(those are structurally unavailable for point samples)."
            ),
            "AssessedPartial": (
                "Road distance available but soil group unknown and/or water distance missing."
            ),
            "Unavailable": "Road proximity unavailable.",
            "legacy_v1_note": (
                "parcels_colombo_osm.csv used Complete/Partial which overstated completeness "
                "when elevation/terrain/overlap were null; v2 uses Assessed* names instead."
            ),
        },
        "feature_definitions": {
            "training_columns": TRAINING_COLUMNS,
            "audit_columns": AUDIT_COLUMNS,
            "gis_derived_fields": [
                "latitude",
                "longitude",
                "province",
                "district",
                "distance_to_road_m",
                "nearest_road_osm_id",
                "nearest_road_highway",
                "nearest_road_adm2_name",
                "distance_to_water_m",
                "water_feature_source",
                "derived_soil_group",
                "spatial_constraint_present",
                "gis_enrichment_status",
            ],
            "unavailable_honest_null_or_unknown": [
                "elevation_meters (no DEM)",
                "soil_overlap_percentage (point sample)",
                "terrain_description (no terrain layer)",
            ],
            "simulated_fields": SIMULATED_FIELDS,
            "label_rule_version": LABEL_RULE_VERSION,
            "label_rules": (
                "Same spirit as generate_synthetic_dataset.rule_based_label: "
                "road/water/soil/area/spatial/gis-status scoring with Gaussian noise; "
                "High/Prohibitive env severity hard-caps Suitable. Soil bonus matches "
                "Alluvial or Reddish Brown substrings in GIS soil names."
            ),
            "semantic_note_distance_to_road_m": (
                "Feature name matches the RF training contract, but meaning is now "
                "filtered OSM motor-road centreline proximity (policy "
                f"{roads_manifest.get('policy_version', POLICY_VERSION)}), not the "
                "synthetic exponential draw and not live expressway-only enrichment."
            ),
        },
        "missing_value_counts": missing_evidence,
        "training_column_null_counts": missing,
        "label_distribution": df[TRAINING_TARGET].value_counts().to_dict(),
        "gis_enrichment_status_counts": df["gis_enrichment_status"].value_counts().to_dict(),
        "limitations": [
            "Samples are random locations, not cadastral parcels.",
            "Labels are simulated, not expert or historical ground truth.",
            "Environmental restriction type/severity are simulated.",
            "Elevation and terrain are unavailable in this GIS pack.",
            "soil_overlap_percentage is unavailable for point samples.",
            "OSM access tags were not exported; road distances are mapped proximity only.",
            "Live RoadAccessibilityEnrichmentService still gates on Hambantota coverage.",
            "This dataset does not retrain or activate the Random Forest model.",
        ],
        "reproducibility_command": (
            "py src/Modules/LandIntelligence/ML/generate_gis_grounded_dataset.py "
            f"--n {n} --district {district_canonical} --seed {seed} "
            f"--out {out_csv.as_posix()}"
        ),
        "training_follow_up": (
            "train_random_forest.py already imputes numeric NaNs with median and "
            "categoricals with Unknown. Honest nulls for elevation_meters and "
            "soil_overlap_percentage are therefore compatible; do not fill them "
            "with zeros. Retrain is out of scope for this step."
        ),
    }
    write_json(out_manifest, manifest)
    return {"dataframe": df, "manifest": manifest, "road_validation": road_validation}


def main(argv: list[str] | None = None) -> int:
    root = resolve_repository_root()
    paths = default_paths(root)
    parser = argparse.ArgumentParser(
        description="Generate Colombo GIS-grounded suitability dataset (offline)."
    )
    parser.add_argument("--n", type=int, default=DEFAULT_N)
    parser.add_argument("--district", type=str, default=DEFAULT_DISTRICT)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument(
        "--out",
        type=Path,
        default=paths["out_csv"],
        help="Output CSV path (must not already exist)",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=None,
        help="Output manifest JSON path (default: sibling *_manifest.json)",
    )
    args = parser.parse_args(argv)

    out_csv = args.out if args.out.is_absolute() else root / args.out
    out_manifest = (
        args.manifest
        if args.manifest is not None
        else out_csv.with_name(out_csv.stem + "_manifest.json")
    )
    if not out_manifest.is_absolute():
        out_manifest = root / out_manifest

    try:
        result = generate_dataset(
            n=args.n,
            district=args.district,
            seed=args.seed,
            out_csv=out_csv,
            out_manifest=out_manifest,
            paths=paths,
        )
    except (FileNotFoundError, FileExistsError, ValueError, RuntimeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    df = result["dataframe"]
    print(f"Wrote {len(df)} rows to {out_csv}")
    print(f"Manifest: {out_manifest}")
    print("Label distribution:")
    print(df[TRAINING_TARGET].value_counts().to_string())
    print("Missing evidence:")
    for key, value in result["manifest"]["missing_value_counts"].items():
        print(f"  {key}: {value}")
    print(
        "Road distance geodesic validation max_abs_error_m=",
        result["road_validation"]["max_abs_error_m"],
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

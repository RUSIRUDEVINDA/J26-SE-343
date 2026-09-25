"""
Reusable helpers for Colombo GIS-grounded suitability dataset generation.

Distance calculations use a local projected CRS (EPSG:32644, UTM zone 44N)
for nearest-neighbour search and metre reporting. Representative cases are
cross-checked against WGS84 geodesic distances (pyproj Geod).
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

import geopandas as gpd
import numpy as np
import pandas as pd
from pyproj import Geod
from shapely.geometry import MultiPolygon, Point, Polygon
from shapely.ops import nearest_points

WGS84 = "EPSG:4326"
# Colombo / Sri Lanka west coast sits in UTM zone 44N.
LOCAL_PROJECTED_CRS = "EPSG:32644"
GEOD = Geod(ellps="WGS84")

TRAINING_NUMERIC = [
    "area_hectares",
    "elevation_meters",
    "distance_to_road_m",
    "distance_to_water_m",
    "soil_overlap_percentage",
]
TRAINING_CATEGORICAL = [
    "requested_purpose",
    "land_category",
    "province",
    "district",
    "gis_enrichment_status",
    "derived_soil_group",
    "terrain_description",
    "environmental_restriction_type",
    "environmental_restriction_severity",
    "spatial_constraint_present",
]
TRAINING_TARGET = "suitability_label"
TRAINING_COLUMNS = TRAINING_NUMERIC + TRAINING_CATEGORICAL + [TRAINING_TARGET]

# Audit / provenance columns — not part of the RF training feature list.
AUDIT_COLUMNS = [
    "parcel_id",
    "sample_kind",
    "latitude",
    "longitude",
    "nearest_road_osm_id",
    "nearest_road_highway",
    "nearest_road_adm2_name",
    "road_distance_method",
    "road_projected_crs",
    "water_feature_source",
    "soil_source_layer",
    "spatial_constraint_source",
    "label_rule_version",
    "simulated_fields",
]


def file_sha256(path: Path, chunk_size: int = 1 << 20) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(chunk_size)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def verify_motor_roads_gpkg(gpkg_path: Path, layer: str = "osm_motor_roads") -> dict[str, Any]:
    """Validate prepared motor-road GeoPackage; require a real rtree spatial index."""
    import sqlite3

    if not gpkg_path.is_file():
        raise FileNotFoundError(f"Motor-road GeoPackage not found: {gpkg_path}")

    con = sqlite3.connect(gpkg_path)
    try:
        contents = con.execute(
            "SELECT table_name, data_type, srs_id FROM gpkg_contents"
        ).fetchall()
        geom_cols = con.execute(
            "SELECT table_name, column_name, geometry_type_name, srs_id "
            "FROM gpkg_geometry_columns"
        ).fetchall()
        rtree_tables = [
            row[0]
            for row in con.execute(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'rtree%'"
            ).fetchall()
        ]
        extensions = []
        if con.execute(
            "SELECT name FROM sqlite_master WHERE name='gpkg_extensions'"
        ).fetchone():
            extensions = con.execute(
                "SELECT extension_name, table_name FROM gpkg_extensions"
            ).fetchall()
    finally:
        con.close()

    layer_rows = [row for row in contents if row[0] == layer]
    if not layer_rows:
        raise ValueError(f"Layer '{layer}' not found in {gpkg_path}")
    if layer_rows[0][2] != 4326:
        raise ValueError(f"Layer '{layer}' srs_id={layer_rows[0][2]}; expected 4326")

    expected_rtree = f"rtree_{layer}_geom"
    if expected_rtree not in rtree_tables:
        raise ValueError(
            f"Spatial index missing: expected SQLite table '{expected_rtree}'. "
            f"Found rtree tables: {rtree_tables}"
        )
    if ("gpkg_rtree_index", layer) not in extensions:
        raise ValueError(
            f"gpkg_rtree_index extension not registered for layer '{layer}'."
        )

    gdf = gpd.read_file(gpkg_path, layer=layer)
    if gdf.crs is None or int(gdf.crs.to_epsg() or 0) != 4326:
        raise ValueError(f"Layer CRS is {gdf.crs}; expected EPSG:4326")
    if gdf.empty:
        raise ValueError(f"Layer '{layer}' is empty")
    invalid = int((~gdf.geometry.is_valid).sum())
    empty = int(gdf.geometry.is_empty.sum())
    if invalid or empty:
        raise ValueError(f"Invalid/empty geometries in motor roads: invalid={invalid}, empty={empty}")

    return {
        "path": str(gpkg_path.resolve()),
        "layer": layer,
        "feature_count": len(gdf),
        "crs_epsg": 4326,
        "geometry_types": dict(gdf.geometry.geom_type.value_counts()),
        "columns": list(gdf.columns),
        "spatial_index_tables": rtree_tables,
        "spatial_index_extension": True,
        "invalid_geometries": invalid,
        "empty_geometries": empty,
    }


def load_district_boundary(
    districts_path: Path,
    district_name: str,
) -> tuple[Polygon | MultiPolygon, str, str]:
    if not districts_path.is_file():
        raise FileNotFoundError(f"District boundaries not found: {districts_path}")
    gdf = gpd.read_file(districts_path)
    if gdf.crs is None or int(gdf.crs.to_epsg() or 0) != 4326:
        raise ValueError(f"District boundaries CRS is {gdf.crs}; expected EPSG:4326")
    if "district_name" not in gdf.columns:
        raise ValueError("district_boundaries.geojson missing district_name")

    match = gdf[gdf["district_name"].astype(str).str.casefold() == district_name.casefold()]
    if match.empty:
        available = sorted(gdf["district_name"].astype(str).unique())
        raise ValueError(
            f"District '{district_name}' not found. Available: {available}"
        )

    geom = match.geometry.union_all()
    if geom.is_empty:
        raise ValueError(f"District '{district_name}' geometry is empty")
    if not isinstance(geom, (Polygon, MultiPolygon)):
        geom = geom.buffer(0)
    province = str(match.iloc[0].get("province_name") or "Unknown")
    return geom, province, str(match.iloc[0]["district_name"])


def sample_points_in_polygon(
    polygon: Polygon | MultiPolygon,
    n: int,
    rng: np.random.Generator,
    max_attempts_factor: int = 80,
) -> list[Point]:
    """Rejection sampling with polygon.contains (respects holes / multipart)."""
    if n <= 0:
        raise ValueError("--n must be positive")
    minx, miny, maxx, maxy = polygon.bounds
    points: list[Point] = []
    attempts = 0
    max_attempts = max(n * max_attempts_factor, n)
    while len(points) < n and attempts < max_attempts:
        remaining = n - len(points)
        batch = max(remaining * 4, 64)
        xs = rng.uniform(minx, maxx, size=batch)
        ys = rng.uniform(miny, maxy, size=batch)
        for x, y in zip(xs, ys):
            attempts += 1
            pt = Point(float(x), float(y))
            if polygon.contains(pt):
                points.append(pt)
                if len(points) >= n:
                    break
    if len(points) < n:
        raise RuntimeError(
            f"Could only sample {len(points)}/{n} points inside the district "
            f"after {attempts} attempts."
        )
    return points


def geodesic_distance_m(a: Point, b) -> float:
    """WGS84 geodesic distance in metres between a point and any geometry."""
    nearest_on_b = nearest_points(a, b)[1]
    _, _, dist = GEOD.inv(a.x, a.y, nearest_on_b.x, nearest_on_b.y)
    return float(abs(dist))


def nearest_features_projected(
    points_wgs84: gpd.GeoDataFrame,
    features_wgs84: gpd.GeoDataFrame,
    value_columns: list[str],
    distance_column: str,
) -> gpd.GeoDataFrame:
    """
    Nearest-neighbour join in LOCAL_PROJECTED_CRS; distance column in metres.
    Uses GeoPandas spatial index via sjoin_nearest.
    """
    if features_wgs84.empty:
        out = points_wgs84.copy()
        out[distance_column] = np.nan
        for col in value_columns:
            out[col] = pd.NA
        return out

    pts = points_wgs84.to_crs(LOCAL_PROJECTED_CRS)
    feats = features_wgs84.to_crs(LOCAL_PROJECTED_CRS)
    joined = gpd.sjoin_nearest(
        pts,
        feats[value_columns + ["geometry"]],
        how="left",
        distance_col=distance_column,
    )
    # sjoin_nearest can duplicate if ties; keep first match per point index.
    joined = joined[~joined.index.duplicated(keep="first")]
    joined = joined.reindex(pts.index)
    return joined


def validate_projected_vs_geodesic(
    points_wgs84: gpd.GeoDataFrame,
    features_wgs84: gpd.GeoDataFrame,
    projected_distances: pd.Series,
    feature_indices: pd.Series,
    sample_size: int = 8,
    max_abs_error_m: float = 5.0,
    rng: np.random.Generator | None = None,
) -> dict[str, Any]:
    """Cross-check projected metre distances against WGS84 geodesic on a sample."""
    rng = rng or np.random.default_rng(0)
    valid = projected_distances.notna() & feature_indices.notna()
    candidate_idx = points_wgs84.index[valid].to_list()
    if not candidate_idx:
        return {"checked": 0, "max_abs_error_m": None, "passed": False, "cases": []}

    take = min(sample_size, len(candidate_idx))
    chosen = rng.choice(candidate_idx, size=take, replace=False)
    cases = []
    max_err = 0.0
    for idx in chosen:
        pt = points_wgs84.geometry.loc[idx]
        feat_idx = int(feature_indices.loc[idx])
        feat_geom = features_wgs84.geometry.loc[feat_idx]
        projected = float(projected_distances.loc[idx])
        geodesic = geodesic_distance_m(pt, feat_geom)
        err = abs(projected - geodesic)
        max_err = max(max_err, err)
        cases.append(
            {
                "point_index": int(idx) if not isinstance(idx, (str,)) else idx,
                "projected_m": round(projected, 3),
                "geodesic_m": round(geodesic, 3),
                "abs_error_m": round(err, 3),
            }
        )
    return {
        "checked": take,
        "max_abs_error_m": round(max_err, 3),
        "tolerance_m": max_abs_error_m,
        "passed": max_err <= max_abs_error_m,
        "cases": cases,
        "projected_crs": LOCAL_PROJECTED_CRS,
        "note": (
            "EPSG:32644 (UTM 44N) planar metres are used for nearest search. "
            "Errors vs WGS84 geodesic are typically sub-metre to a few metres "
            "within Sri Lanka; do not use EPSG:4326 degree distances."
        ),
    }


def load_water_features(canals_path: Path, lakes_path: Path) -> gpd.GeoDataFrame:
    frames: list[gpd.GeoDataFrame] = []
    for path, kind, name_col in (
        (canals_path, "canal", "canal_name"),
        (lakes_path, "lake", "lake_name"),
    ):
        if not path.is_file():
            continue
        gdf = gpd.read_file(path)
        if gdf.crs is None:
            gdf = gdf.set_crs(WGS84)
        elif int(gdf.crs.to_epsg() or 0) != 4326:
            gdf = gdf.to_crs(WGS84)
        gdf = gdf[~gdf.geometry.is_empty & gdf.geometry.notna()].copy()
        gdf["water_feature_source"] = kind
        gdf["water_feature_name"] = gdf[name_col] if name_col in gdf.columns else pd.NA
        frames.append(gdf[["water_feature_source", "water_feature_name", "geometry"]])
    if not frames:
        return gpd.GeoDataFrame(
            columns=["water_feature_source", "water_feature_name", "geometry"],
            geometry="geometry",
            crs=WGS84,
        )
    return gpd.GeoDataFrame(pd.concat(frames, ignore_index=True), crs=WGS84)


def point_soil_group(
    points: gpd.GeoDataFrame,
    soil_path: Path,
) -> pd.Series:
    """
    Soil group name under each point. Points with no soil polygon stay Unknown.
    Does not invent soil_overlap_percentage (parcel overlap cannot be inferred
    from a sample point).
    """
    if not soil_path.is_file():
        return pd.Series(["Unknown"] * len(points), index=points.index)
    soil = gpd.read_file(soil_path)
    if soil.crs is None:
        soil = soil.set_crs(WGS84)
    elif int(soil.crs.to_epsg() or 0) != 4326:
        soil = soil.to_crs(WGS84)
    if "name" not in soil.columns:
        raise ValueError("soil_groups.geojson missing 'name' column")
    soil = soil[["name", "geometry"]].rename(columns={"name": "derived_soil_group"})
    joined = gpd.sjoin(points, soil, how="left", predicate="within")
    joined = joined[~joined.index.duplicated(keep="first")].reindex(points.index)
    return joined["derived_soil_group"].fillna("Unknown")


def point_in_conservation(
    points: gpd.GeoDataFrame,
    conservation_path: Path,
) -> pd.Series:
    """
    True if sample point intersects an imported soil conservation polygon.

    False means no intersection with the *available* conservation layer
    (verified absence of mapped intersection within that layer), not that
    all real-world spatial constraints are absent, and not Unknown.
    """
    if not conservation_path.is_file():
        return pd.Series([False] * len(points), index=points.index)
    areas = gpd.read_file(conservation_path)
    if areas.crs is None:
        areas = areas.set_crs(WGS84)
    elif int(areas.crs.to_epsg() or 0) != 4326:
        areas = areas.to_crs(WGS84)
    joined = gpd.sjoin(points, areas[["geometry"]], how="left", predicate="intersects")
    # Left join keeps every point; only rows with a matched polygon are hits.
    hit_ids = set(joined.index[joined["index_right"].notna()])
    return pd.Series([idx in hit_ids for idx in points.index], index=points.index)


def rule_based_simulated_label(row: pd.Series, rng: np.random.Generator) -> str:
    """
    Simulated suitability label (NOT observed ground truth).

    Same scoring spirit as generate_synthetic_dataset.rule_based_label, adapted
    only so real GIS soil names can match the Alluvial substring used in the
    synthetic heuristic. Thresholds are unchanged.
    """
    score = 0.0
    purpose = row["requested_purpose"]
    severity = row["environmental_restriction_severity"]
    hard_block = severity in ("Prohibitive", "High")

    road = row["distance_to_road_m"]
    if pd.notna(road):
        if purpose in ("Commercial", "Residential"):
            score += 2.0 if road < 300 else (1.0 if road < 1000 else -1.0)
        else:
            score += 1.0 if road < 1500 else 0.0

    water = row["distance_to_water_m"]
    if pd.notna(water):
        if purpose == "Agricultural":
            score += 2.0 if water < 500 else (1.0 if water < 1500 else -0.5)
        else:
            score += 0.5 if water < 2000 else 0.0

    soil = row["derived_soil_group"]
    if purpose == "Agricultural" and soil not in ("Unknown", None) and pd.notna(soil):
        soil_s = str(soil)
        if "Alluvial" in soil_s or "Reddish Brown" in soil_s:
            score += 1.5

    area = row["area_hectares"]
    min_area = {"Agricultural": 1.0, "Residential": 0.05, "Commercial": 0.1}[purpose]
    score += 1.0 if area >= min_area else -1.5

    if row["spatial_constraint_present"] in (True, "True", "true"):
        score -= 1.0

    status = str(row["gis_enrichment_status"])
    if status in ("Partial", "AssessedPartial"):
        score -= 0.5
    elif status == "Unavailable":
        score -= 1.5

    score += rng.normal(0, 0.9)

    if hard_block:
        return "Unsuitable" if score < 1.5 else "Moderately Suitable"
    if score >= 2.5:
        return "Suitable"
    if score >= 0.0:
        return "Moderately Suitable"
    return "Unsuitable"


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")

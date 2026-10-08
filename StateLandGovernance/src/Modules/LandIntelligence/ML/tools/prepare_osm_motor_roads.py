"""
Prepare nationwide OSM motor-road linework for the Colombo GIS/ML experiment.

Reads the existing HOT/oex export (roads.gpkg) without modifying it, filters to
valid non-empty line geometries and the mapped motor-road highway allowlist,
and writes a separate GeoPackage with a spatial index.

Usage (from repo root, after installing tools requirements):
    py -m pip install -r StateLandGovernance/src/Modules/LandIntelligence/ML/tools/requirements-tools.txt
    py StateLandGovernance/src/Modules/LandIntelligence/ML/tools/prepare_osm_motor_roads.py

Does not import into PostgreSQL or change the live Land Intelligence backend.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

import geopandas as gpd
from shapely.geometry.base import BaseGeometry

from osm_motor_road_policy import (
    DISTANCE_METHOD_DOC,
    HIGHWAY_ALLOWLIST,
    HIGHWAY_EXCLUDED_EXPLICIT,
    POLICY_VERSION,
    PROXIMITY_DISCLAIMER,
    SOURCE_CRS_EPSG,
    SOURCE_DATASET,
    VALID_LINE_GEOM_TYPES,
)

DEFAULT_REL_SOURCE = Path("data/gis/LandIntelligence_GIS/osm/roads.gpkg")
DEFAULT_REL_OUTPUT_DIR = Path("data/gis/experiments/colombo")
OUTPUT_GPKG_NAME = "osm_motor_roads.gpkg"
OUTPUT_LAYER = "osm_motor_roads"
MANIFEST_NAME = "osm_motor_roads_manifest.json"

NAME_COLUMNS = ("name", "name_en", "name_latin", "name_si", "name_ta")
PROVENANCE_COLUMNS = (
    "adm0_pcode",
    "adm0_name",
    "adm1_pcode",
    "adm1_name",
    "adm2_pcode",
    "adm2_name",
)


def resolve_repository_root(start: Path | None = None) -> Path:
    """Directory containing StateLandGovernance.sln (the .NET solution / data root)."""
    start = start or Path(__file__).resolve()
    for directory in [start, *start.parents]:
        if (directory / "StateLandGovernance.sln").exists():
            return directory
    raise FileNotFoundError(
        "Could not locate Land Intelligence data root (expected StateLandGovernance.sln)."
    )


def load_source_provenance(readme_path: Path) -> dict[str, str | None]:
    if not readme_path.is_file():
        return {
            "source": "OpenStreetMap contributors",
            "source_url": "https://www.openstreetmap.org/",
            "snapshot": None,
            "license": "hdx-odc-odbl",
            "license_url": "https://opendatacommons.org/licenses/odbl/1-0/",
            "export_readme": str(readme_path),
        }

    text = readme_path.read_text(encoding="utf-8")
    fields: dict[str, str | None] = {
        "source": "OpenStreetMap contributors",
        "source_url": "https://www.openstreetmap.org/",
        "snapshot": None,
        "license": "hdx-odc-odbl",
        "license_url": "https://opendatacommons.org/licenses/odbl/1-0/",
        "export_readme": str(readme_path),
    }
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("Snapshot:"):
            fields["snapshot"] = stripped.split(":", 1)[1].strip()
        elif stripped.startswith("License:") and "URL" not in stripped:
            fields["license"] = stripped.split(":", 1)[1].strip()
        elif stripped.startswith("License URL:"):
            fields["license_url"] = stripped.split(":", 1)[1].strip()
        elif stripped.startswith("Source URL:"):
            fields["source_url"] = stripped.split(":", 1)[1].strip()
    return fields


def geometry_type_counts(gdf: gpd.GeoDataFrame) -> dict[str, int]:
    return dict(Counter(gdf.geometry.geom_type))


def is_valid_non_empty_line(geom: BaseGeometry | None) -> bool:
    if geom is None or geom.is_empty:
        return False
    if geom.geom_type not in VALID_LINE_GEOM_TYPES:
        return False
    return geom.is_valid


def prepare_motor_roads(
    source_gpkg: Path,
    output_dir: Path,
    layer: str = SOURCE_DATASET,
) -> dict:
    if not source_gpkg.is_file():
        raise FileNotFoundError(f"Source GeoPackage not found: {source_gpkg}")

    raw = gpd.read_file(source_gpkg, layer=layer)
    if raw.empty:
        raise ValueError(f"Source layer '{layer}' is empty in {source_gpkg}")

    input_geom_types = geometry_type_counts(raw)
    input_highway_counts = (
        dict(Counter(raw["highway"].dropna().astype(str)))
        if "highway" in raw.columns
        else {}
    )

    if raw.crs is None:
        raise ValueError("Source layer has no CRS; expected EPSG:4326.")
    if int(raw.crs.to_epsg() or 0) != SOURCE_CRS_EPSG:
        raise ValueError(
            f"Source CRS is {raw.crs}; expected EPSG:{SOURCE_CRS_EPSG}."
        )

    if "highway" not in raw.columns:
        raise ValueError("Source layer is missing required column 'highway'.")
    if "id" not in raw.columns:
        raise ValueError("Source layer is missing required OSM feature column 'id'.")

    highways = raw["highway"].astype(str).str.strip()
    allow_mask = highways.isin(HIGHWAY_ALLOWLIST)
    excluded_hits = highways.isin(HIGHWAY_EXCLUDED_EXPLICIT)
    filtered = raw.loc[allow_mask].copy()

    line_mask = filtered.geometry.apply(is_valid_non_empty_line)
    dropped_invalid = int((~line_mask).sum())
    roads = filtered.loc[line_mask].copy()

    if roads.empty:
        raise ValueError(
            "No motor-road features remained after highway and geometry filtering. "
            f"Allowlist matches before geometry filter: {int(allow_mask.sum())}."
        )

    keep_columns = ["id", "highway", *NAME_COLUMNS, *PROVENANCE_COLUMNS]
    missing = [c for c in keep_columns if c not in roads.columns]
    if missing:
        raise ValueError(f"Source layer missing expected columns: {missing}")

    roads = roads[keep_columns + ["geometry"]].copy()
    roads = roads.rename(columns={"id": "osm_id"})

    output_dir.mkdir(parents=True, exist_ok=True)
    output_gpkg = output_dir / OUTPUT_GPKG_NAME
    if output_gpkg.exists():
        output_gpkg.unlink()

    try:
        import pyogrio

        pyogrio.write_dataframe(
            roads,
            output_gpkg,
            layer=OUTPUT_LAYER,
            driver="GPKG",
            spatial_index=True,
        )
    except TypeError:
        roads.to_file(output_gpkg, layer=OUTPUT_LAYER, driver="GPKG", engine="pyogrio")

    written = gpd.read_file(output_gpkg, layer=OUTPUT_LAYER)
    if written.crs is None or int(written.crs.to_epsg() or 0) != SOURCE_CRS_EPSG:
        raise ValueError("Output GeoPackage CRS validation failed.")
    if not all(gt in VALID_LINE_GEOM_TYPES for gt in written.geometry.geom_type.unique()):
        raise ValueError("Output contains non-line geometry types.")
    if written.empty:
        raise ValueError("Output GeoPackage is empty after write verification.")

    output_highway_counts = dict(Counter(written["highway"].astype(str)))
    readme_path = source_gpkg.parent / "README.txt"
    provenance = load_source_provenance(readme_path)

    manifest = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "policy_version": POLICY_VERSION,
        "purpose": "Colombo experiment — mapped motor-road proximity (offline preparation only)",
        "proximity_disclaimer": PROXIMITY_DISCLAIMER,
        "distance_method": DISTANCE_METHOD_DOC,
        "source": {
            "path": str(source_gpkg.resolve()),
            "layer": layer,
            "crs_epsg": SOURCE_CRS_EPSG,
            "unchanged": True,
            **provenance,
        },
        "filter": {
            "highway_allowlist": sorted(HIGHWAY_ALLOWLIST),
            "highway_excluded_documented": sorted(HIGHWAY_EXCLUDED_EXPLICIT),
            "geometry_retained": sorted(VALID_LINE_GEOM_TYPES),
            "geometry_dropped_non_line_or_empty_or_invalid": dropped_invalid,
            "excluded_explicit_highway_hits_in_source": int(excluded_hits.sum()),
            "features_matching_allowlist_before_geometry_filter": int(allow_mask.sum()),
        },
        "output": {
            "path": str(output_gpkg.resolve()),
            "layer": OUTPUT_LAYER,
            "crs_epsg": SOURCE_CRS_EPSG,
            "spatial_index": True,
            "feature_count": len(written),
            "highway_counts": output_highway_counts,
        },
        "input_summary": {
            "feature_count": len(raw),
            "geometry_types": input_geom_types,
            "highway_counts_top": dict(
                sorted(input_highway_counts.items(), key=lambda kv: kv[1], reverse=True)[:20]
            ),
        },
        "integration_notes": {
            "live_backend_unchanged": True,
            "postgresql_import": False,
            "hambantota_pilot_coverage_gate": (
                "RoadAccessibilityEnrichmentService currently returns Unavailable "
                "for parcels outside imported Hambantota district coverage. Colombo "
                "integration must relax or replace that gate explicitly."
            ),
        },
    }

    manifest_path = output_dir / MANIFEST_NAME
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    return manifest


def main(argv: list[str] | None = None) -> int:
    repo_root = resolve_repository_root()
    parser = argparse.ArgumentParser(
        description="Filter OSM roads.gpkg to mapped motor-road linework (Colombo experiment)."
    )
    parser.add_argument(
        "--source",
        type=Path,
        default=repo_root / DEFAULT_REL_SOURCE,
        help="Path to source roads.gpkg (default: bundled LandIntelligence_GIS export)",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=repo_root / DEFAULT_REL_OUTPUT_DIR,
        help="Directory for prepared GeoPackage and manifest",
    )
    parser.add_argument(
        "--layer",
        default=SOURCE_DATASET,
        help="Source GeoPackage layer name",
    )
    args = parser.parse_args(argv)

    try:
        manifest = prepare_motor_roads(args.source, args.output_dir, layer=args.layer)
    except (FileNotFoundError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    out = manifest["output"]
    print(f"Wrote {out['feature_count']} features to {out['path']}")
    print(f"Manifest: {args.output_dir / MANIFEST_NAME}")
    print("Highway counts (retained):")
    for highway, count in sorted(out["highway_counts"].items(), key=lambda kv: kv[1], reverse=True):
        print(f"  {highway}: {count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""
Export prepared osm_motor_roads.gpkg to GeoJSON for C# / PostGIS import tooling.

Does not import into PostgreSQL. Output is a separate GeoJSON file.
Supports optional district buffer clipping for practical local-dev imports while
still including roads beyond the Colombo boundary.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

import geopandas as gpd

from osm_motor_road_policy import POLICY_VERSION, PROXIMITY_DISCLAIMER, SOURCE_CRS_EPSG
from prepare_osm_motor_roads import resolve_repository_root

DEFAULT_GPKG = Path("data/gis/experiments/colombo/osm_motor_roads.gpkg")
DEFAULT_LAYER = "osm_motor_roads"
DEFAULT_OUT = Path("data/gis/experiments/colombo/osm_motor_roads.geojson")
DEFAULT_BUFFERED_OUT = Path(
    "data/gis/experiments/colombo/osm_motor_roads_colombo_buffer.geojson"
)


def main(argv: list[str] | None = None) -> int:
    root = resolve_repository_root()
    parser = argparse.ArgumentParser(description="Export OSM motor roads GeoPackage to GeoJSON")
    parser.add_argument("--source", type=Path, default=root / DEFAULT_GPKG)
    parser.add_argument("--layer", default=DEFAULT_LAYER)
    parser.add_argument("--out", type=Path, default=None)
    parser.add_argument(
        "--district-buffer-km",
        type=float,
        default=None,
        help="If set, clip to Colombo district + buffer (km) so nearest-road can still "
        "cross the boundary without exporting the full nationwide layer.",
    )
    parser.add_argument("--district-name", default="Colombo")
    args = parser.parse_args(argv)

    source = args.source if args.source.is_absolute() else root / args.source
    if args.out is None:
        out = root / (
            DEFAULT_BUFFERED_OUT if args.district_buffer_km is not None else DEFAULT_OUT
        )
    else:
        out = args.out if args.out.is_absolute() else root / args.out

    if not source.is_file():
        print(f"ERROR: source not found: {source}", file=sys.stderr)
        return 1

    gdf = gpd.read_file(source, layer=args.layer)
    if gdf.crs is None or int(gdf.crs.to_epsg() or 0) != SOURCE_CRS_EPSG:
        print(f"ERROR: expected EPSG:{SOURCE_CRS_EPSG}, got {gdf.crs}", file=sys.stderr)
        return 1
    if gdf.empty:
        print("ERROR: empty layer", file=sys.stderr)
        return 1

    clip_note = "Nationwide (or full prepared layer); not clipped."
    if args.district_buffer_km is not None:
        boundaries = gpd.read_file(
            root / "data/gis/LandIntelligence_GIS/boundaries/district_boundaries.geojson"
        )
        district = boundaries[
            boundaries["district_name"].astype(str).str.casefold()
            == args.district_name.casefold()
        ]
        if district.empty:
            print(f"ERROR: district '{args.district_name}' not found", file=sys.stderr)
            return 1
        buffer_deg = float(args.district_buffer_km) / 111.0
        clip_geom = district.union_all() if hasattr(district, "union_all") else district.unary_union
        clip_geom = clip_geom.buffer(buffer_deg)
        before = len(gdf)
        gdf = gdf[gdf.intersects(clip_geom)].copy()
        clip_note = (
            f"Clipped to {args.district_name} + {args.district_buffer_km} km buffer "
            f"({before} -> {len(gdf)} features). Roads beyond the district remain when "
            f"they fall inside the buffer."
        )
        if gdf.empty:
            print("ERROR: clip removed all features", file=sys.stderr)
            return 1

    gdf = gdf.copy()
    gdf["filter_policy_version"] = POLICY_VERSION
    gdf["proximity_disclaimer"] = PROXIMITY_DISCLAIMER

    out.parent.mkdir(parents=True, exist_ok=True)
    gdf.to_file(out, driver="GeoJSON")

    sidecar = {
        "exported_at_utc": datetime.now(timezone.utc).isoformat(),
        "source_gpkg": str(source.resolve()),
        "layer": args.layer,
        "feature_count": len(gdf),
        "crs_epsg": SOURCE_CRS_EPSG,
        "filter_policy_version": POLICY_VERSION,
        "output": str(out.resolve()),
        "clip": clip_note,
        "note": "For local-dev C# OsmMotorRoadImportService / isolated PostGIS verification.",
    }
    sidecar_path = out.with_suffix(".export.json")
    sidecar_path.write_text(json.dumps(sidecar, indent=2), encoding="utf-8")
    print(f"Wrote {len(gdf)} features to {out}")
    print(f"Sidecar: {sidecar_path}")
    print(clip_note)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

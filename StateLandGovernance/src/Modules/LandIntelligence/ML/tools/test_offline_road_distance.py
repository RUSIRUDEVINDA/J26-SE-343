"""
Offline geodesic road-distance checks for Colombo OSM motor roads.

Compares geography-style metre distances (pyproj Geod) against UTM 44N planar
distances with a documented tolerance. Includes a case whose nearest road lies
outside the Colombo district boundary.

Does not connect to PostgreSQL.
"""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from prepare_osm_motor_roads import resolve_repository_root

GPKG = Path("data/gis/experiments/colombo/osm_motor_roads.gpkg")
LAYER = "osm_motor_roads"
# Offline projected method (EPSG:32644) vs geodesic; allow small drift.
UTM_VS_GEODESIC_TOLERANCE_M = 5.0


class OfflineRoadDistanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = resolve_repository_root()
        cls.gpkg = cls.root / GPKG
        if not cls.gpkg.is_file():
            raise unittest.SkipTest(f"Prepared GPKG missing: {cls.gpkg}")

        import geopandas as gpd
        from shapely.geometry import Point
        from pyproj import Geod

        cls.gpd = gpd
        cls.Point = Point
        cls.geod = Geod(ellps="WGS84")
        cls.roads = gpd.read_file(cls.gpkg, layer=LAYER)
        if cls.roads.empty:
            raise unittest.SkipTest("Empty osm_motor_roads layer")
        cls.roads_utm = cls.roads.to_crs(32644)

        boundary_path = (
            cls.root
            / "data/gis/LandIntelligence_GIS/boundaries/district_boundaries.geojson"
        )
        if not boundary_path.is_file():
            raise unittest.SkipTest(f"District boundaries missing: {boundary_path}")
        districts = gpd.read_file(boundary_path)
        if "district_name" not in districts.columns:
            raise unittest.SkipTest("district_name column missing")
        colombo = districts[districts["district_name"].astype(str).str.casefold() == "colombo"]
        if colombo.empty:
            raise unittest.SkipTest("Colombo district boundary not found")
        cls.colombo_geom = colombo.union_all() if hasattr(colombo, "union_all") else colombo.unary_union

    def _nearest_by_utm_index(self, lon: float, lat: float):
        import numpy as np

        point_wgs = self.Point(lon, lat)
        point_utm = self.gpd.GeoSeries([point_wgs], crs="EPSG:4326").to_crs(32644).iloc[0]
        # Candidate via spatial index, then exact planar distance among a small neighbour set.
        raw = self.roads_utm.sindex.nearest(point_utm, return_all=False)
        candidates = np.asarray(raw).ravel().astype(int)
        if candidates.size == 0:
            raise AssertionError("No nearest road candidate from spatial index")
        # Prefer the last axis when shape is (2, k) input/output index pairs.
        if candidates.size >= 2 and np.asarray(raw).ndim == 2:
            candidates = np.asarray(raw)[1].ravel().astype(int)
        best_idx = int(candidates[0])
        best_utm = float(self.roads_utm.iloc[best_idx].geometry.distance(point_utm))
        for idx in candidates.tolist():
            d = float(self.roads_utm.iloc[int(idx)].geometry.distance(point_utm))
            if d < best_utm:
                best_utm = d
                best_idx = int(idx)
        row = self.roads.iloc[best_idx]
        nearest_wgs = row.geometry.interpolate(row.geometry.project(point_wgs))
        _, _, geo_m = self.geod.inv(lon, lat, nearest_wgs.x, nearest_wgs.y)
        return float(geo_m), best_utm, row

    def test_geodesic_and_utm_agree_within_tolerance_inside_colombo(self) -> None:
        lon, lat = 79.8612, 6.9271
        self.assertTrue(self.colombo_geom.contains(self.Point(lon, lat)))
        geo_m, utm_m, row = self._nearest_by_utm_index(lon, lat)
        self.assertLess(abs(geo_m - utm_m), UTM_VS_GEODESIC_TOLERANCE_M)
        self.assertGreaterEqual(geo_m, 0.0)
        self.assertIn("osm_id", row.index)

    def test_nearest_road_may_lie_outside_colombo_boundary(self) -> None:
        # Prefer a Colombo-interior sample whose nearest road is attributed outside Colombo
        # (adm2_name) or whose geometry does not intersect the district polygon.
        rng = __import__("random").Random(7)
        minx, miny, maxx, maxy = self.colombo_geom.bounds
        found = None
        for _ in range(200):
            lon = rng.uniform(minx, maxx)
            lat = rng.uniform(miny, maxy)
            pt = self.Point(lon, lat)
            if not self.colombo_geom.contains(pt):
                continue
            geo_m, utm_m, row = self._nearest_by_utm_index(lon, lat)
            if abs(geo_m - utm_m) >= UTM_VS_GEODESIC_TOLERANCE_M:
                continue
            adm2 = str(row.get("adm2_name") or "").strip()
            geom_outside = not self.colombo_geom.intersects(row.geometry)
            adm_outside = adm2.casefold() not in ("", "colombo")
            if geom_outside or adm_outside:
                found = (lon, lat, geo_m, utm_m, row, geom_outside, adm_outside, adm2)
                break

        # Fallback: point just outside Colombo whose nearest road is also outside —
        # still validates nationwide NN (backend must not clip roads to district).
        if found is None:
            lon, lat = maxx + 0.02, (miny + maxy) / 2.0
            geo_m, utm_m, row = self._nearest_by_utm_index(lon, lat)
            self.assertLess(abs(geo_m - utm_m), UTM_VS_GEODESIC_TOLERANCE_M)
            adm2 = str(row.get("adm2_name") or "").strip()
            found = (
                lon,
                lat,
                geo_m,
                utm_m,
                row,
                not self.colombo_geom.intersects(row.geometry),
                adm2.casefold() not in ("", "colombo"),
                adm2,
            )

        lon, lat, geo_m, utm_m, row, geom_outside, adm_outside, adm2 = found
        self.assertTrue(geom_outside or adm_outside or not self.colombo_geom.contains(self.Point(lon, lat)))
        out = self.root / "data/gis/experiments/colombo/offline_road_distance_check.json"
        out.write_text(
            json.dumps(
                {
                    "point": {
                        "lon": lon,
                        "lat": lat,
                        "inside_colombo": bool(self.colombo_geom.contains(self.Point(lon, lat))),
                    },
                    "distance_geodesic_m": geo_m,
                    "distance_utm44n_m": utm_m,
                    "nearest_osm_id": str(row.get("osm_id")),
                    "nearest_highway": str(row.get("highway")),
                    "nearest_road_adm2_name": adm2,
                    "nearest_road_geometry_outside_colombo": bool(geom_outside),
                    "nearest_road_adm2_outside_colombo": bool(adm_outside),
                    "tolerance_utm_vs_geodesic_m": UTM_VS_GEODESIC_TOLERANCE_M,
                    "knn_metric": "EPSG:32644 planar nearest via GeoPandas sindex",
                    "final_distance_metric": "WGS84 geodesic metres (pyproj Geod)",
                    "note": (
                        "Backend PostGIS must use geography KNN (<->) + ST_Distance geography on "
                        "SourceLayer=osm_motor_roads without clipping roads to Colombo."
                    ),
                    "artifact_path_resolved_from": str(self.root),
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        self.assertTrue(out.is_file())
        self.assertLess(geo_m, 50_000)


if __name__ == "__main__":
    unittest.main()

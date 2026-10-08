"""Focused validation for Colombo GIS-grounded dataset helpers."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

import geopandas as gpd
import numpy as np
from shapely.geometry import LineString, Point, Polygon

_TOOLS = Path(__file__).resolve().parent
if str(_TOOLS) not in sys.path:
    sys.path.insert(0, str(_TOOLS))

from gis_grounded_dataset_helpers import (
    TRAINING_COLUMNS,
    geodesic_distance_m,
    nearest_features_projected,
    sample_points_in_polygon,
    validate_projected_vs_geodesic,
    verify_motor_roads_gpkg,
)
from prepare_osm_motor_roads import resolve_repository_root


class SampleGeometryTests(unittest.TestCase):
    def test_samples_stay_inside_polygon_with_hole(self):
        outer = [(0, 0), (0, 10), (10, 10), (10, 0), (0, 0)]
        hole = [(3, 3), (3, 7), (7, 7), (7, 3), (3, 3)]
        poly = Polygon(outer, [hole])
        rng = np.random.default_rng(0)
        pts = sample_points_in_polygon(poly, 40, rng)
        self.assertEqual(40, len(pts))
        for pt in pts:
            self.assertTrue(poly.contains(pt))
            self.assertFalse(Point(pt).within(Polygon(hole)))


class DistanceTests(unittest.TestCase):
    def test_projected_nearest_matches_geodesic_within_tolerance(self):
        points = gpd.GeoDataFrame(
            geometry=[Point(79.86, 6.93), Point(79.90, 6.95)],
            crs="EPSG:4326",
        )
        roads = gpd.GeoDataFrame(
            {
                "osm_id": ["a", "b"],
                "highway": ["primary", "residential"],
            },
            geometry=[
                LineString([(79.85, 6.92), (79.87, 6.94)]),
                LineString([(80.10, 7.20), (80.12, 7.22)]),
            ],
            crs="EPSG:4326",
        )
        joined = nearest_features_projected(
            points, roads, ["osm_id", "highway"], "distance_to_road_m"
        )
        self.assertTrue((joined["distance_to_road_m"] >= 0).all())
        self.assertEqual("a", joined.iloc[0]["osm_id"])

        validation = validate_projected_vs_geodesic(
            points,
            roads,
            joined["distance_to_road_m"],
            joined["index_right"],
            sample_size=2,
            max_abs_error_m=5.0,
            rng=np.random.default_rng(1),
        )
        self.assertTrue(validation["passed"], validation)

    def test_nearest_road_may_lie_outside_district_bbox(self):
        # Point inside a small "district"; nearest road is outside that box.
        district = Polygon([(79.86, 6.92), (79.86, 6.94), (79.88, 6.94), (79.88, 6.92)])
        point = Point(79.87, 6.93)
        self.assertTrue(district.contains(point))
        roads = gpd.GeoDataFrame(
            {"osm_id": ["outside"], "highway": ["trunk"], "adm2_name": ["Gampaha"]},
            geometry=[LineString([(79.95, 6.93), (79.96, 6.93)])],
            crs="EPSG:4326",
        )
        points = gpd.GeoDataFrame(geometry=[point], crs="EPSG:4326")
        joined = nearest_features_projected(
            points, roads, ["osm_id", "highway", "adm2_name"], "distance_to_road_m"
        )
        self.assertEqual("outside", joined.iloc[0]["osm_id"])
        self.assertEqual("Gampaha", joined.iloc[0]["adm2_name"])
        self.assertFalse(district.contains(roads.geometry.iloc[0]))
        self.assertGreater(float(joined.iloc[0]["distance_to_road_m"]), 0)

    def test_geodesic_helper_non_negative(self):
        d = geodesic_distance_m(Point(79.86, 6.93), Point(79.87, 6.93))
        self.assertGreater(d, 0)


class PreparedRoadsIntegrationTests(unittest.TestCase):
    def test_verify_prepared_gpkg_when_present(self):
        root = resolve_repository_root()
        gpkg = root / "data/gis/experiments/colombo/osm_motor_roads.gpkg"
        if not gpkg.is_file():
            self.skipTest("prepared motor-road GeoPackage not present")
        info = verify_motor_roads_gpkg(gpkg)
        self.assertEqual(262911, info["feature_count"])
        self.assertTrue(info["spatial_index_extension"])
        self.assertTrue(any(name.startswith("rtree_") for name in info["spatial_index_tables"]))


class ConservationJoinTests(unittest.TestCase):
    def test_left_join_does_not_mark_all_points_as_hits(self):
        from gis_grounded_dataset_helpers import point_in_conservation
        import tempfile

        points = gpd.GeoDataFrame(
            geometry=[Point(79.87, 6.93), Point(79.88, 6.94)],
            crs="EPSG:4326",
        )
        # Tiny conservation polygon covering only the first point.
        areas = gpd.GeoDataFrame(
            geometry=[
                Polygon(
                    [
                        (79.869, 6.929),
                        (79.869, 6.931),
                        (79.871, 6.931),
                        (79.871, 6.929),
                    ]
                )
            ],
            crs="EPSG:4326",
        )
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "cons.geojson"
            areas.to_file(path, driver="GeoJSON")
            hits = point_in_conservation(points, path)
        self.assertTrue(bool(hits.iloc[0]))
        self.assertFalse(bool(hits.iloc[1]))


class TrainingContractTests(unittest.TestCase):
    def test_training_columns_match_trainer_expectations(self):
        # Mirrors train_random_forest.py NUMERIC + CATEGORICAL + TARGET
        expected = {
            "area_hectares",
            "elevation_meters",
            "distance_to_road_m",
            "distance_to_water_m",
            "soil_overlap_percentage",
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
            "suitability_label",
        }
        self.assertEqual(expected, set(TRAINING_COLUMNS))


class ColomboTrainFeatureTests(unittest.TestCase):
    def test_colombo_experiment_excludes_unusable_and_audit_fields(self):
        ml_dir = Path(__file__).resolve().parents[1]
        sys.path.insert(0, str(ml_dir))
        import train_colombo_osm as tco

        predictors = set(tco.NUMERIC_FEATURES + tco.CATEGORICAL_FEATURES)
        for field in tco.AUDIT_EXCLUDE:
            self.assertNotIn(field, predictors)
        for field in (
            "elevation_meters",
            "soil_overlap_percentage",
            "terrain_description",
            "province",
            "district",
        ):
            self.assertNotIn(field, predictors)
        self.assertIn("distance_to_road_m", predictors)


if __name__ == "__main__":
    unittest.main()

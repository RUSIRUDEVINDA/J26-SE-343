"""Focused validation for OSM motor-road preparation policy and helpers."""

import unittest
from pathlib import Path

from osm_motor_road_policy import (
    HIGHWAY_ALLOWLIST,
    HIGHWAY_EXCLUDED_EXPLICIT,
    POLICY_VERSION,
    SOURCE_CRS_EPSG,
)
from prepare_osm_motor_roads import (
    is_valid_non_empty_line,
    resolve_repository_root,
)
from shapely.geometry import LineString, Point


class OsmMotorRoadPolicyTests(unittest.TestCase):
    def test_allowlist_and_exclusions_disjoint(self):
        overlap = HIGHWAY_ALLOWLIST & HIGHWAY_EXCLUDED_EXPLICIT
        self.assertEqual(frozenset(), overlap)

    def test_excluded_highways_not_in_allowlist(self):
        for tag in HIGHWAY_EXCLUDED_EXPLICIT:
            self.assertNotIn(tag, HIGHWAY_ALLOWLIST, msg=tag)

    def test_residential_in_allowlist(self):
        self.assertIn("residential", HIGHWAY_ALLOWLIST)

    def test_policy_version_present(self):
        self.assertTrue(POLICY_VERSION)

    def test_source_crs(self):
        self.assertEqual(4326, SOURCE_CRS_EPSG)


class GeometryValidationTests(unittest.TestCase):
    def test_accepts_linestring(self):
        self.assertTrue(is_valid_non_empty_line(LineString([(0, 0), (1, 1)])))

    def test_rejects_point(self):
        self.assertFalse(is_valid_non_empty_line(Point(0, 0)))

    def test_rejects_empty_line(self):
        self.assertFalse(is_valid_non_empty_line(LineString()))


class RepositoryRootTests(unittest.TestCase):
    def test_resolve_repository_root_finds_sln(self):
        root = resolve_repository_root(Path(__file__).resolve())
        self.assertTrue((root / "StateLandGovernance.sln").exists())


if __name__ == "__main__":
    unittest.main()

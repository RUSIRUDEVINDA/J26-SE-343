"""
OSM motor-road filter policy for Component 1 Colombo experiment road preparation.

Distances derived from this layer represent mapped motor-road proximity only.
OpenStreetMap access tags (e.g. access=private) are not present in the HOT export;
this is not verified public vehicle access.
"""

from __future__ import annotations

POLICY_VERSION = "2026-03-20-colombo-v1"

SOURCE_DATASET = "roads"
SOURCE_CRS_EPSG = 4326

# Initial mapped motor-road allowlist (highway tag values).
HIGHWAY_ALLOWLIST: frozenset[str] = frozenset(
    {
        "motorway",
        "motorway_link",
        "trunk",
        "trunk_link",
        "primary",
        "primary_link",
        "secondary",
        "secondary_link",
        "tertiary",
        "tertiary_link",
        "residential",
        "unclassified",
        "service",
        "living_street",
    }
)

# Documented exclusions for this policy (not in allowlist; also rejected if present).
HIGHWAY_EXCLUDED_EXPLICIT: frozenset[str] = frozenset(
    {
        "track",
        "path",
        "footway",
        "cycleway",
        "steps",
        "proposed",
        "construction",
    }
)

VALID_LINE_GEOM_TYPES: frozenset[str] = frozenset({"LineString", "MultiLineString"})

DISTANCE_METHOD_DOC = (
    "Nearest-road distances for experiments should use geodesic metres on the "
    "WGS84 ellipsoid (EPSG:4326 geometries cast to geography), e.g. "
    "ST_Distance(geom::geography, road.geom::geography) in PostGIS or "
    "equivalent geodesic measure in GIS libraries. Output CRS remains EPSG:4326; "
    "planar degree distance must not be used for metre reporting."
)

PROXIMITY_DISCLAIMER = (
    "Mapped motor-road proximity from filtered OpenStreetMap highway linework. "
    "Not verified public vehicle access (access restrictions were not exported)."
)

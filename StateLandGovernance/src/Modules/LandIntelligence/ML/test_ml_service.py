"""Unit tests for ML API missing-data handling (stdlib only)."""

import unittest

from ml_service import MODEL_FEATURE_COLUMNS, ParcelFeatures, _to_model_row, predict


class MlServiceMissingDataTests(unittest.TestCase):
    def test_schema_accepts_null_gis_numerics(self):
        features = ParcelFeatures(
            requested_purpose="Agricultural",
            land_category="StateLand",
            area_hectares=5.0,
            province="Western",
            district="Colombo",
            gis_enrichment_status="Unavailable",
            elevation_meters=None,
            distance_to_road_m=None,
            distance_to_water_m=None,
            derived_soil_group=None,
            soil_overlap_percentage=None,
            terrain_description=None,
            environmental_restriction_type=None,
            environmental_restriction_severity=None,
            spatial_constraint_present=None,
        )

        self.assertIsNone(features.distance_to_road_m)
        self.assertIsNone(features.derived_soil_group)

    def test_schema_accepts_unknown_categoricals(self):
        features = ParcelFeatures(
            requested_purpose="Agricultural",
            land_category="StateLand",
            area_hectares=5.0,
            province="Western",
            district="Colombo",
            gis_enrichment_status="Unavailable",
            derived_soil_group="Unknown",
            terrain_description="Unknown",
        )

        self.assertEqual("Unknown", features.derived_soil_group)
        self.assertEqual("Unknown", features.terrain_description)

    def test_model_row_excludes_road_water_soil(self):
        features = ParcelFeatures(
            requested_purpose="Agricultural",
            land_category="StateLand",
            area_hectares=5.0,
            province="Western",
            district="Colombo",
            gis_enrichment_status="Unavailable",
            distance_to_road_m=12.0,
            distance_to_water_m=34.0,
            derived_soil_group="Alluvial",
            soil_overlap_percentage=80.0,
            terrain_description="Unknown",
        )

        row = _to_model_row(features)

        self.assertEqual(list(row.columns), MODEL_FEATURE_COLUMNS)
        for excluded in (
            "distance_to_road_m",
            "distance_to_water_m",
            "derived_soil_group",
            "soil_overlap_percentage",
        ):
            self.assertNotIn(excluded, row.columns)
        self.assertTrue(row["elevation_meters"].isna().iloc[0])
        self.assertTrue(row["spatial_constraint_present"].isna().iloc[0])

    def test_predict_accepts_missing_gis_payload(self):
        features = ParcelFeatures(
            requested_purpose="Agricultural",
            land_category="StateLand",
            area_hectares=5.0,
            province="Western",
            district="Colombo",
            gis_enrichment_status="Unavailable",
            derived_soil_group="Unknown",
            terrain_description="Unknown",
            environmental_restriction_type="None",
            environmental_restriction_severity="None",
            spatial_constraint_present=False,
        )

        response = predict(features)

        self.assertIn(
            response.predicted_label,
            {"Unsuitable", "Moderately Suitable", "Suitable"},
        )
        self.assertGreater(len(response.probabilities), 0)
        self.assertIn("distance_to_road_m", response.ignored_unavailable_features)


if __name__ == "__main__":
    unittest.main()

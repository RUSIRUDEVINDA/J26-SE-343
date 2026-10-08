using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Builds the Colombo experimental ML feature payload from persisted GIS evidence.
/// Does not call the ML service or activate the experimental model.
/// </summary>
public interface IExperimentalColomboMlFeatureAdapter
{
    ExperimentalColomboMlFeaturePayload BuildPayload(
        LandParcel parcel,
        LandUseType requestedPurpose,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental,
        LandParcelGisEnrichmentOverallStatus? overallStatus = null);
}

/// <summary>
/// Imports prepared OSM motor-road GeoJSON into gis_roads (SourceLayer = osm_motor_roads).
/// </summary>
public interface IOsmMotorRoadImportService
{
    Task<OsmMotorRoadImportResult> ImportFromGeoJsonAsync(
        string geoJsonPath,
        string? filterPolicyVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a district boundary from LandIntelligence_GIS district_boundaries.geojson
    /// (required for Colombo coverage checks). Uses real boundary geometry.
    /// </summary>
    Task<int> ImportDistrictBoundaryAsync(
        string districtName,
        string? dataRootPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes only SourceLayer=osm_motor_roads rows (rollback for local-dev).</summary>
    Task<int> DeleteOsmMotorRoadsAsync(CancellationToken cancellationToken = default);
}

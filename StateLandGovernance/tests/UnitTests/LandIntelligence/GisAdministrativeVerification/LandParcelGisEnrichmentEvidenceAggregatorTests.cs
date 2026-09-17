using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class LandParcelGisEnrichmentEvidenceAggregatorTests
{
    [Fact]
    public void Aggregate_describes_nearest_mapped_road_not_generic_road_network()
    {
        var evidence = LandParcelGisEnrichmentEvidenceAggregator.Aggregate(
            administrative: null,
            road: new RoadAccessibilityEnrichmentResult
            {
                ParcelId = Guid.NewGuid(),
                RoadName = "Southern Expressway",
                RoadType = GisReferenceRoadType.Expressway,
                DistanceMeters = 2400d,
                GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
                Status = RoadAccessibilityEnrichmentStatus.Available,
                Evidence = [],
                SourceName = "LandIntelligence_GIS",
                SourceLayer = "expressways"
            },
            water: null,
            soil: null,
            environmental: null);

        Assert.Contains(
            evidence,
            item => item.Contains("nearest mapped Expressway", StringComparison.Ordinal));
        Assert.DoesNotContain(
            evidence,
            item => item.Contains("Sri Lanka", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Aggregate_states_erosion_evidence_is_unavailable_without_claiming_safety()
    {
        var evidence = LandParcelGisEnrichmentEvidenceAggregator.Aggregate(
            administrative: null,
            road: null,
            water: null,
            soil: null,
            environmental: new EnvironmentalSpatialConstraintEnrichmentResult
            {
                ParcelId = Guid.NewGuid(),
                Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
                IntersectsSoilConservationArea = false,
                ConservationAreas = [],
                ErosionDataStatus = ErosionDataStatus.Unavailable,
                ErosionObservations = [],
                Evidence = [],
                SourceName = "LandIntelligence_GIS",
                SourceLayer = "soil_conservation_areas, soil_erosion"
            });

        Assert.Contains(
            evidence,
            item => item.Contains("soil erosion observation evidence is unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            evidence,
            item => item.Contains("must not be interpreted as low erosion risk", StringComparison.OrdinalIgnoreCase));
    }
}

using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class EnvironmentalSpatialConstraintEnrichmentEvaluatorTests
{
    [Fact]
    public void ToConservationAreaEvidence_maps_all_boundary_overlaps()
    {
        var overlaps = new List<SoilConservationOverlapCandidate>
        {
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Area B",
                "Desc B",
                "Source",
                "soil_conservation_areas",
                OverlapAreaSquareMeters: 100,
                OverlapPercentage: 25m),
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Area A",
                "Desc A",
                "Source",
                "soil_conservation_areas",
                OverlapAreaSquareMeters: 200,
                OverlapPercentage: 50m)
        };

        var evidence = EnvironmentalSpatialConstraintEnrichmentEvaluator.ToConservationAreaEvidence(overlaps);

        Assert.Equal(2, evidence.Count);
        Assert.Equal("Area A", evidence[0].Name);
        Assert.Equal(50m, evidence[0].OverlapPercentage);
        Assert.Equal("Area B", evidence[1].Name);
        Assert.Equal(25m, evidence[1].OverlapPercentage);
    }

    [Fact]
    public void ToConservationAreaEvidence_maps_all_centroid_matches_without_overlap_metrics()
    {
        var matches = new List<SoilConservationCentroidCandidate>
        {
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Area A",
                "Desc A",
                "Source",
                "soil_conservation_areas"),
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Area B",
                null,
                "Source",
                "soil_conservation_areas")
        };

        var evidence = EnvironmentalSpatialConstraintEnrichmentEvaluator.ToConservationAreaEvidence(matches);

        Assert.Equal(2, evidence.Count);
        Assert.All(evidence, item => Assert.Null(item.OverlapPercentage));
        Assert.All(evidence, item => Assert.Null(item.OverlapAreaSquareMeters));
    }

    [Fact]
    public void ToErosionObservationEvidence_orders_by_distance()
    {
        var observations = new List<SoilErosionObservationCandidate>
        {
            new(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Far",
                null,
                null,
                "Source",
                "soil_erosion",
                DistanceMeters: 800),
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Near",
                null,
                null,
                "Source",
                "soil_erosion",
                DistanceMeters: 100)
        };

        var evidence = EnvironmentalSpatialConstraintEnrichmentEvaluator.ToErosionObservationEvidence(observations);

        Assert.Equal("Near", evidence[0].ObservationClass);
        Assert.Equal("Far", evidence[1].ObservationClass);
    }

    [Fact]
    public void ErosionDataStatus_Unavailable_does_not_imply_environmental_safety()
    {
        var result = new EnvironmentalSpatialConstraintEnrichmentResult
        {
            ParcelId = Guid.NewGuid(),
            Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
            GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = ErosionDataStatus.Unavailable,
            ErosionObservations = [],
            Evidence =
            [
                "Soil erosion GIS observations are unavailable in the imported pilot dataset. " +
                "Absence of GIS observations must not be interpreted as evidence that the parcel is environmentally safe."
            ],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_conservation_areas, soil_erosion"
        };

        Assert.Equal(ErosionDataStatus.Unavailable, result.ErosionDataStatus);
        Assert.Empty(result.ErosionObservations);
        Assert.DoesNotContain(
            result.Evidence,
            item => item.Contains("low risk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            result.Evidence,
            item => item.Contains("no erosion", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.Evidence,
            item => item.Contains("must not be interpreted", StringComparison.OrdinalIgnoreCase));
    }
}

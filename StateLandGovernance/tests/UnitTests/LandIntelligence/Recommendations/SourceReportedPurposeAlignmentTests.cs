using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class SourceReportedPurposeAlignmentTests
{
    [Theory]
    [InlineData(LandUseType.Agricultural, "73", "Paddy Field", true, 100)]
    [InlineData(LandUseType.Agricultural, "2", "Garden", true, 70)]
    [InlineData(LandUseType.Residential, "1", "Homegarden", true, 100)]
    [InlineData(LandUseType.Commercial, "1", "Homegarden", false, 30)]
    [InlineData(LandUseType.Agricultural, "53", "Road", false, 15)]
    [InlineData(LandUseType.Residential, "25", "Stream", false, 15)]
    public void Purpose_alignment_uses_source_reported_land_use_for_scoring_only(
        LandUseType purpose,
        string code,
        string label,
        bool expectMet,
        int expectScore)
    {
        var parcel = CreateRealCadastreParcel("REAL-PURPOSE-001");
        var context = RecommendationEngineTestFactory.CreateParcelContext(new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [parcel.Id] = CreateSource(code, label)
        });
        var evaluator = new PurposeAlignmentCriterionEvaluator(context);

        var result = evaluator.Evaluate(parcel, new LandRecommendationSearchRequest
        {
            RequiredPurpose = purpose
        });

        Assert.Equal(expectMet, result.IsMet);
        Assert.Equal(expectScore, result.Score);
        Assert.Null(parcel.CurrentUse);
        Assert.DoesNotContain("current use", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_purpose_changes_ranking_when_source_land_use_differs()
    {
        var paddy = CreateRealCadastreParcel("REAL-PADDY");
        var home = CreateRealCadastreParcel("REAL-HOME");
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [paddy.Id] = CreateSource("73", "Paddy Field"),
            [home.Id] = CreateSource("1", "Homegarden")
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(sources, paddy, home);

        var agricultural = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10
        });
        var residential = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Residential,
            MaxResults = 10
        });

        Assert.Equal("REAL-PADDY", agricultural.Recommendations[0].CadastralNumber);
        Assert.Equal("REAL-HOME", residential.Recommendations[0].CadastralNumber);
        Assert.NotEqual(
            agricultural.Recommendations[0].SuitabilityScore,
            residential.Recommendations.Single(r => r.CadastralNumber == "REAL-PADDY").SuitabilityScore);
    }

    [Fact]
    public async Task District_min_area_and_source_land_use_filters_affect_results()
    {
        var inDistrict = CreateRealCadastreParcel("REAL-IN", district: "Colombo", areaHa: 2m, roadMeters: 400m);
        var outDistrict = CreateRealCadastreParcel("REAL-OUT", district: "Gampaha", areaHa: 2m, roadMeters: 400m);
        var tooSmall = CreateRealCadastreParcel("REAL-SMALL", district: "Colombo", areaHa: 0.2m, roadMeters: 400m);
        var farRoad = CreateRealCadastreParcel("REAL-FAR", district: "Colombo", areaHa: 2m, roadMeters: 9000m);
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [inDistrict.Id] = CreateSource("73", "Paddy Field"),
            [outDistrict.Id] = CreateSource("73", "Paddy Field"),
            [tooSmall.Id] = CreateSource("73", "Paddy Field"),
            [farRoad.Id] = CreateSource("73", "Paddy Field")
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(
            sources,
            inDistrict,
            outDistrict,
            tooSmall,
            farRoad);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            PreferredLocation = new PreferredLocationCriteria { District = "Colombo" },
            TargetAreaHectares = 1m,
            SourceReportedLandUse = "73",
            MaxResults = 10
        });

        // Target area scores parcels but does not exclude smaller parcels; road distance is not a filter.
        Assert.Equal(3, response.CandidateCount);
        Assert.Contains(response.Recommendations, r => r.CadastralNumber == "REAL-IN");
        Assert.Contains(response.Recommendations, r => r.CadastralNumber == "REAL-FAR");
        Assert.Contains(response.Recommendations, r => r.CadastralNumber == "REAL-SMALL");
        Assert.DoesNotContain(response.Recommendations, r => r.CadastralNumber == "REAL-OUT");
        Assert.NotNull(response.Transparency);
        Assert.Contains(response.Transparency!.FilterStages, s => s.Name == "District");
        Assert.DoesNotContain(response.Transparency.FilterStages, s => s.Name == "Minimum area");
        Assert.DoesNotContain(response.Transparency.FilterStages, s => s.Name == "Maximum road distance");
    }

    [Fact]
    public async Task Missing_road_or_water_gis_is_not_scored_in_recommendation_flow()
    {
        var parcel = CreateRealCadastreParcel("REAL-NO-ROAD", roadMeters: null);
        parcel.ReplaceInfrastructureFeatures([]);
        var engine = RecommendationEngineTestFactory.CreateWithSources(
            new Dictionary<Guid, CadastralSourceMetadataDto>
            {
                [parcel.Id] = CreateSource("73", "Paddy Field")
            },
            parcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.DoesNotContain(
            recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria),
            c => c.Key is "accessibility" or "water-proximity" or "soil-group");
    }

    private static LandParcel CreateRealCadastreParcel(
        string cadastralNumber,
        string district = "Colombo",
        decimal areaHa = 5m,
        decimal? roadMeters = 250m)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "REAL-PLAN"),
            new LandCategory(LandCategoryType.Other, "Cadastre import — not domain-classified"),
            new LandArea(areaHa, AreaUnit.Hectares),
            new AdministrativeLocation("Western", district, $"{district} DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            currentUse: null);

        if (roadMeters is not null)
        {
            parcel.AddInfrastructureFeature(new InfrastructureFeature(
                InfrastructureFeatureType.Road,
                "Mapped Road",
                roadMeters,
                "GIS road distance",
                AttributeProvenance.Derived("GIS road nearest-neighbor spatial query (geography metres)")));
        }

        return parcel;
    }

    private static CadastralSourceMetadataDto CreateSource(string code, string label) =>
        new(
            "Survey Department/iLAS public GIS",
            "Cadastre_Parcels_Landuse_classified",
            "feature-1",
            code,
            label,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow)
        {
            SourceReportedLandUseDisplay = label
        };
}

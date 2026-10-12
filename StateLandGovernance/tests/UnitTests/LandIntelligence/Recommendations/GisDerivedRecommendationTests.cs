using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class GisDerivedRecommendationTests
{
    [Fact]
    public async Task RecommendAsync_does_not_filter_candidates_by_gis_road_distance()
    {
        var near = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(3000m, "GIS-ROAD-3KM");
        var far = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(8000m, "GIS-ROAD-8KM");
        var engine = RecommendationEngineTestFactory.Create(near, far);

        var response = await engine.RecommendAsync(CreateRequest() with { MaxResults = 5 });

        Assert.Equal(2, response.CandidateCount);
        Assert.Equal(2, response.Recommendations.Count);
        Assert.DoesNotContain(
            response.Recommendations.SelectMany(r => r.MatchingCriteria.Concat(r.FailedCriteria)),
            c => c.Key is "accessibility" or "water-proximity" or "soil-group");
    }

    [Fact]
    public async Task RecommendAsync_custom_criteria_can_match_official_soil_without_overwriting()
    {
        const string officialSoil = "Commissioner Loam";
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedSoil(
            officialSoil,
            "Red Yellow Latosols",
            "GIS-SOIL-OFFICIAL");

        Assert.Equal(officialSoil, parcel.Characteristics!.SoilType);
        Assert.Equal("Red Yellow Latosols", parcel.GisDerivedIntelligence!.DerivedSoilGroup!.SoilGroupName);

        var engine = RecommendationEngineTestFactory.Create(parcel);
        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "official-soil",
                    Name = "Official Soil",
                    ParcelAttributePath = "characteristics.soiltype",
                    ExpectedValue = officialSoil,
                    Weight = 0.05m
                }
            ]
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Contains(recommendation.MatchingCriteria, c => c.Key == "custom-criteria");
    }

    [Fact]
    public async Task RecommendAsync_custom_criteria_can_match_gis_derived_soil_group()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedSoil(
            "Commissioner Loam",
            "Red Yellow Latosols",
            "GIS-SOIL-DERIVED");

        var engine = RecommendationEngineTestFactory.Create(parcel);
        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "gis-soil",
                    Name = "GIS Soil Group",
                    ParcelAttributePath = "characteristics.gisderivedsoilgroup",
                    ExpectedValue = "Red Yellow Latosols",
                    Weight = 0.05m
                }
            ]
        });

        Assert.Single(response.Recommendations);
    }

    [Fact]
    public async Task RecommendAsync_official_and_differing_gis_soil_both_remain_distinct()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedSoil(
            "Commissioner Loam",
            "Red Yellow Latosols",
            "GIS-SOIL-DIFF");

        var engine = RecommendationEngineTestFactory.Create(parcel);
        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "official-soil",
                    Name = "Official Soil",
                    ParcelAttributePath = "characteristics.soiltype",
                    ExpectedValue = "Commissioner Loam",
                    Weight = 0.05m
                },
                new CustomCriterionCriteria
                {
                    Key = "gis-soil",
                    Name = "GIS Soil Group",
                    ParcelAttributePath = "characteristics.gisderivedsoilgroup",
                    ExpectedValue = "Red Yellow Latosols",
                    Weight = 0.05m
                }
            ]
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Contains(recommendation.MatchingCriteria, c => c.Key == "custom-criteria");
        Assert.Equal("Commissioner Loam", parcel.Characteristics!.SoilType);
        Assert.Equal("Red Yellow Latosols", parcel.GisDerivedIntelligence!.DerivedSoilGroup!.SoilGroupName);
    }

    [Fact]
    public async Task RecommendAsync_gis_conservation_restriction_triggers_hard_constraint_rejection()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisConservationRestriction();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.True(recommendation.HardConstraintRejected);
        Assert.Equal(0m, recommendation.SuitabilityScore);
        Assert.Contains("conservation area", recommendation.HardConstraintReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(recommendation.Evidence, e => e.RelatedCriterionName == "HardConstraintRejection");
        Assert.DoesNotContain(
            recommendation.Evidence,
            e => e.RelatedCriterionName == "MlSuitabilityPrediction");
    }

    [Fact]
    public async Task RecommendAsync_no_conservation_does_not_claim_safe_environment()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("GIS-NO-CONSERVATION");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.False(recommendation.HardConstraintRejected);
        Assert.DoesNotContain(recommendation.Explanation, "safe", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria),
            c => c.Key == "environmental");
    }

    [Fact]
    public async Task RecommendAsync_partial_gis_enrichment_notes_erosion_unavailable_without_penalty()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(3000m, "GIS-PARTIAL");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Contains("erosion observations are unavailable", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.True(recommendation.SuitabilityScore > 0m);
    }

    [Fact]
    public async Task RecommendAsync_unavailable_gis_enrichment_does_not_fabricate_gis_evidence()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithUnavailableGisEnrichment();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.DoesNotContain(recommendation.Evidence, e => e.RelatedCriterionName == "GIS-Derived Intelligence");
        Assert.DoesNotContain("GIS context", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_does_not_rank_by_gis_road_distance()
    {
        var nearer = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(2000m, "GIS-RANK-NEAR");
        var farther = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(7500m, "GIS-RANK-FAR");

        var engine = RecommendationEngineTestFactory.Create(nearer, farther);

        var response = await engine.RecommendAsync(CreateRequest() with { MaxResults = 2 });

        Assert.Equal(2, response.Recommendations.Count);
        Assert.DoesNotContain(
            response.Recommendations.SelectMany(r => r.MatchingCriteria.Concat(r.FailedCriteria)),
            c => c.Key == "accessibility");
    }

    private static LandRecommendationSearchRequest CreateRequest() => new()
    {
        RequiredPurpose = LandUseType.Agricultural,
        RequiredAreaHectares = 1m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        MaxResults = 5
    };
}


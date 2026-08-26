using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class GisDerivedRecommendationTests
{
    [Fact]
    public async Task RecommendAsync_keeps_candidate_when_gis_road_is_3km_and_max_is_5km()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(3000m, "GIS-ROAD-3KM");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRoadFilterRequest(5000m) with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Contains("Nearest mapped road", recommendation.MatchingCriteria.Single(c => c.Key == "accessibility").Summary);
    }

    [Fact]
    public async Task RecommendAsync_excludes_candidate_when_gis_road_is_8km_and_max_is_5km()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(8000m, "GIS-ROAD-8KM");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRoadFilterRequest(5000m));

        Assert.Empty(response.Recommendations);
        Assert.Equal(0, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_excludes_when_hospital_near_and_gis_road_8km_despite_water_other_nearby()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedNaturalWaterOnly(200m, "GIS-MIXED-BASE");
        parcel.ReplaceInfrastructureFeatures([
            new InfrastructureFeature(
                InfrastructureFeatureType.WaterSupply,
                "[SYNTHETIC] Hospital",
                500m),
            new InfrastructureFeature(
                InfrastructureFeatureType.Other,
                "[GIS-DERIVED] Canal",
                200m,
                "[GIS-DERIVED] Natural water proximity.",
                AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName)),
            new InfrastructureFeature(
                InfrastructureFeatureType.Road,
                "[GIS-DERIVED] Mapped Road",
                8000m,
                "[GIS-DERIVED] Nearest mapped road.",
                AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName))
        ]);

        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRoadFilterRequest(5000m));

        Assert.Empty(response.Recommendations);
    }

    [Fact]
    public async Task RecommendAsync_natural_water_other_record_does_not_satisfy_road_accessibility()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedNaturalWaterOnly(500m);
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRoadFilterRequest(5000m));

        Assert.Empty(response.Recommendations);
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
    public async Task RecommendAsync_gis_conservation_restriction_affects_environmental_criterion_only()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisConservationRestriction();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        var environmental = Assert.Single(
            recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria),
            c => c.Key == "environmental");
        Assert.Contains("mapped conservation area", environmental.Summary, StringComparison.OrdinalIgnoreCase);

        var spatial = Assert.Single(
            recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria),
            c => c.Key == "spatial-constraints");
        Assert.Contains("No spatial constraints", spatial.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_no_conservation_does_not_claim_safe_environment()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("GIS-NO-CONSERVATION");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        var environmental = Assert.Single(recommendation.MatchingCriteria, c => c.Key == "environmental");
        Assert.Contains("No environmental restrictions", environmental.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safe", environmental.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("erosion", environmental.Summary, StringComparison.OrdinalIgnoreCase);
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
    public async Task RecommendAsync_ranks_closer_gis_road_higher_than_distant_gis_road()
    {
        var nearer = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(2000m, "GIS-RANK-NEAR");
        var farther = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(7500m, "GIS-RANK-FAR");
        farther.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.Wetland,
            "[SYNTHETIC] Additional environmental review required",
            RestrictionSeverity.Medium));

        var engine = RecommendationEngineTestFactory.Create(nearer, farther);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            MaxResults = 2,
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true,
                MaxRoadDistanceMeters = 10000m
            }
        });

        Assert.Equal(2, response.Recommendations.Count);
        var nearerResult = response.Recommendations.Single(result => result.ParcelId == nearer.Id);
        var fartherResult = response.Recommendations.Single(result => result.ParcelId == farther.Id);
        Assert.True(nearerResult.SuitabilityScore > fartherResult.SuitabilityScore);
        Assert.Equal(nearer.Id, response.Recommendations[0].ParcelId);
    }

    private static LandRecommendationSearchRequest CreateRoadFilterRequest(decimal maxRoadDistanceMeters) =>
        new()
        {
            RequiredPurpose = LandUseType.Agricultural,
            Accessibility = new AccessibilityCriteria
            {
                MaxRoadDistanceMeters = maxRoadDistanceMeters
            },
            MaxResults = 5
        };

    private static LandRecommendationSearchRequest CreateRequest() => new()
    {
        RequiredPurpose = LandUseType.Agricultural,
        RequiredAreaHectares = 1m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        Accessibility = new AccessibilityCriteria
        {
            RequireRoadAccess = true,
            MaxRoadDistanceMeters = 10000m
        },
        Environmental = new EnvironmentalCriteria(),
        MaxResults = 5
    };
}

using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class RuleBasedLandRecommendationEngineTests
{
    [Fact]
    public async Task RecommendAsync_returns_explainable_result_for_suitable_parcel()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Equal(parcel.Id, recommendation.ParcelId);
        Assert.True(recommendation.SuitabilityScore >= 70m);
        Assert.Equal(1, recommendation.Rank);
        Assert.NotEmpty(recommendation.MatchingCriteria);
        Assert.NotEmpty(recommendation.Evidence);
        Assert.Contains("suitability score", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Positive factors", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_marks_restricted_parcel_with_lower_score_and_restrictions()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateRestrictedParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            RequiredLandCategory = LandCategoryType.ReservedLand,
            RequiredLandUse = LandUseType.Conservation,
            RequiredPurpose = LandUseType.Conservation
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.True(recommendation.SuitabilityScore < 70m);
        Assert.NotEmpty(recommendation.FailedCriteria);
        Assert.NotEmpty(recommendation.Restrictions);
        Assert.Contains(recommendation.Restrictions, r => r.Severity >= RestrictionSeverity.High);
        Assert.Contains("Restrictions", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_ranks_multiple_candidates_by_score()
    {
        var suitable = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-RANK-001");
        var moderate = SyntheticRecommendationParcelFactory.CreateModerateParcel("SYNTH-RANK-002");
        var restricted = SyntheticRecommendationParcelFactory.CreateRestrictedParcel("SYNTH-RANK-003");
        var engine = RecommendationEngineTestFactory.Create(suitable, moderate, restricted);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 3
        });

        Assert.Equal(3, response.Recommendations.Count);
        Assert.Equal(3, response.CandidateCount);
        Assert.Equal(1, response.Recommendations[0].Rank);
        Assert.Equal(2, response.Recommendations[1].Rank);
        Assert.Equal(3, response.Recommendations[2].Rank);
        Assert.True(response.Recommendations[0].SuitabilityScore >= response.Recommendations[1].SuitabilityScore);
        Assert.True(response.Recommendations[1].SuitabilityScore >= response.Recommendations[2].SuitabilityScore);
    }

    [Fact]
    public async Task RecommendAsync_includes_failed_criteria_for_mismatched_category()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            RequiredLandCategory = LandCategoryType.CrownLand
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Contains(recommendation.FailedCriteria, c => c.Key == "land-category");
        Assert.Contains("Negative factors", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_generates_explanation_with_positive_and_negative_factors()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateModerateParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            PreferredLocation = new PreferredLocationCriteria
            {
                Province = "Western",
                District = "Colombo"
            },
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true,
                MaxRoadDistanceMeters = 500m
            }
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.False(string.IsNullOrWhiteSpace(recommendation.Explanation));
        Assert.Contains(parcel.Identifier.CadastralNumber, recommendation.Explanation);
        Assert.True(
            recommendation.Explanation.Contains("Positive factors", StringComparison.OrdinalIgnoreCase)
            || recommendation.Explanation.Contains("Negative factors", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecommendAsync_evaluates_custom_criteria()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil",
                    Name = "Soil Type",
                    ExpectedValue = "Loam",
                    ParcelAttributePath = "characteristics.soiltype",
                    Weight = 0.05m
                }
            ]
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Contains(recommendation.MatchingCriteria, c => c.Key == "custom-criteria");
    }

    [Fact]
    public async Task RecommendAsync_includes_parcel_larger_than_required_minimum_area()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(14.8m, "SYNTH-AREA-148");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(requiredAreaHectares: 10m));

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Equal(parcel.Id, recommendation.ParcelId);
        Assert.Equal(1, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_includes_parcel_matching_required_minimum_area()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(10m, "SYNTH-AREA-100");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(requiredAreaHectares: 10m));

        Assert.Single(response.Recommendations);
        Assert.Equal(1, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_excludes_parcel_below_required_minimum_area()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(7m, "SYNTH-AREA-070");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(requiredAreaHectares: 10m));

        Assert.Empty(response.Recommendations);
        Assert.Equal(0, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_includes_parcel_within_tolerance_below_required_minimum_area()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(8.7m, "SYNTH-AREA-087");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(
            requiredAreaHectares: 10m,
            areaTolerancePercent: 15m));

        Assert.Single(response.Recommendations);
        Assert.Equal(1, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_includes_parcel_well_above_required_minimum_when_tolerance_applies()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(20m, "SYNTH-AREA-200");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(
            requiredAreaHectares: 10m,
            areaTolerancePercent: 15m));

        Assert.Single(response.Recommendations);
        Assert.Equal(1, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_places_oversized_required_area_in_matching_criteria()
    {
        var parcel148 = SyntheticRecommendationParcelFactory.CreateParcelWithArea(14.8m, "SYNTH-AREA-148-MATCH");
        var parcel225 = SyntheticRecommendationParcelFactory.CreateParcelWithArea(22.5m, "SYNTH-AREA-225-MATCH");
        var engine = RecommendationEngineTestFactory.Create(parcel148, parcel225);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(requiredAreaHectares: 10m));

        foreach (var recommendation in response.Recommendations)
        {
            var requiredArea = Assert.Single(
                recommendation.MatchingCriteria,
                c => c.Key == "required-area");

            Assert.True(requiredArea.IsMet);
            Assert.DoesNotContain(recommendation.FailedCriteria, c => c.Key == "required-area");
        }
    }

    [Fact]
    public async Task RecommendAsync_keeps_parcels_within_max_road_distance_threshold()
    {
        var parcelA = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(2000m, "SYNTH-ROAD-2KM");
        var parcelB = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(4000m, "SYNTH-ROAD-4KM");
        var engine = RecommendationEngineTestFactory.Create(parcelA, parcelB);

        var response = await engine.RecommendAsync(CreateRoadDistanceFilterRequest(maxRoadDistanceMeters: 5000m));

        Assert.Equal(2, response.CandidateCount);
        Assert.Equal(2, response.Recommendations.Count);
        Assert.Contains(response.Recommendations, r => r.ParcelId == parcelA.Id);
        Assert.Contains(response.Recommendations, r => r.ParcelId == parcelB.Id);
    }

    [Fact]
    public async Task RecommendAsync_excludes_parcels_beyond_max_road_distance_threshold()
    {
        var parcelA = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(2000m, "SYNTH-ROAD-2KM-MIX");
        var parcelB = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(8000m, "SYNTH-ROAD-8KM-MIX");
        var engine = RecommendationEngineTestFactory.Create(parcelA, parcelB);

        var response = await engine.RecommendAsync(CreateRoadDistanceFilterRequest(maxRoadDistanceMeters: 5000m));

        Assert.Equal(1, response.CandidateCount);
        var recommendation = Assert.Single(response.Recommendations);
        Assert.Equal(parcelA.Id, recommendation.ParcelId);
    }

    [Fact]
    public async Task RecommendAsync_returns_no_candidates_when_all_parcels_exceed_max_road_distance()
    {
        var parcelA = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(7000m, "SYNTH-ROAD-7KM");
        var parcelB = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(10000m, "SYNTH-ROAD-10KM");
        var engine = RecommendationEngineTestFactory.Create(parcelA, parcelB);

        var response = await engine.RecommendAsync(CreateRoadDistanceFilterRequest(maxRoadDistanceMeters: 5000m));

        Assert.Empty(response.Recommendations);
        Assert.Equal(0, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_includes_parcel_at_exact_max_road_distance_boundary()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(5000m, "SYNTH-ROAD-5KM");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRoadDistanceFilterRequest(maxRoadDistanceMeters: 5000m));

        Assert.Single(response.Recommendations);
        Assert.Equal(1, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_does_not_apply_road_distance_filter_when_max_distance_not_specified()
    {
        var parcelA = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(2000m, "SYNTH-ROAD-NOFILTER-A");
        var parcelB = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(12000m, "SYNTH-ROAD-NOFILTER-B");
        var engine = RecommendationEngineTestFactory.Create(parcelA, parcelB);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true
            },
            MaxResults = 5
        });

        Assert.Equal(2, response.CandidateCount);
        Assert.Equal(2, response.Recommendations.Count);
    }

    [Fact]
    public async Task RecommendAsync_excludes_parcel_when_road_distance_data_is_unavailable()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(null, "SYNTH-ROAD-NODATA");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRoadDistanceFilterRequest(maxRoadDistanceMeters: 5000m));

        Assert.Empty(response.Recommendations);
        Assert.Equal(0, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_ranks_parcel_without_environmental_restrictions_above_prohibitive_parcel()
    {
        var suitable = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-ENV-RANK-SUITABLE");
        var restricted = SyntheticRecommendationParcelFactory.CreateParcelWithEnvironmentalRestriction(
            EnvironmentalRestrictionType.Wetland,
            RestrictionSeverity.Prohibitive,
            "SYNTH-ENV-RANK-RESTRICTED");
        var engine = RecommendationEngineTestFactory.Create(suitable, restricted);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            PreferredLocation = new PreferredLocationCriteria
            {
                Province = "Western"
            },
            Environmental = new EnvironmentalCriteria
            {
                MaxAllowedEnvironmentalSeverity = RestrictionSeverity.Medium,
                RejectProhibitiveEnvironmentalRestrictions = true
            },
            MaxResults = 2
        });

        Assert.Equal(2, response.Recommendations.Count);
        Assert.Equal(suitable.Id, response.Recommendations[0].ParcelId);
        Assert.True(response.Recommendations[0].SuitabilityScore > response.Recommendations[1].SuitabilityScore);
        Assert.Contains(response.Recommendations[1].FailedCriteria, c => c.Key == "environmental");
    }

    [Fact]
    public async Task RecommendAsync_returns_unique_standard_criterion_keys()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id
        });

        var recommendation = Assert.Single(response.Recommendations);
        var criterionKeys = recommendation.MatchingCriteria
            .Concat(recommendation.FailedCriteria)
            .Select(c => c.Key)
            .ToList();

        Assert.Equal(criterionKeys.Count, criterionKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task RecommendAsync_returns_unique_rule_based_evidence_per_criterion()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id
        });

        var recommendation = Assert.Single(response.Recommendations);
        var ruleBasedEvidenceNames = recommendation.Evidence
            .Where(e => e.Source == "RuleBasedCriterionEvaluator")
            .Select(e => e.RelatedCriterionName)
            .ToList();

        Assert.Equal(
            ruleBasedEvidenceNames.Count,
            ruleBasedEvidenceNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task RecommendAsync_evaluates_each_applicable_standard_criterion_once()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id
        });

        var recommendation = Assert.Single(response.Recommendations);
        var evaluatedCriteria = recommendation.MatchingCriteria
            .Concat(recommendation.FailedCriteria)
            .ToList();

        Assert.Equal(9, evaluatedCriteria.Count);
        Assert.Contains(evaluatedCriteria, c => c.Key == "purpose-alignment");
        Assert.Contains(evaluatedCriteria, c => c.Key == "required-area");
        Assert.Contains(evaluatedCriteria, c => c.Key == "land-category");
        Assert.Contains(evaluatedCriteria, c => c.Key == "land-use");
        Assert.Contains(evaluatedCriteria, c => c.Key == "location-preference");
        Assert.Contains(evaluatedCriteria, c => c.Key == "spatial-constraints");
    }

    [Fact]
    public async Task RecommendAsync_preserves_multiple_custom_criteria_under_single_custom_evaluator()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil-match",
                    Name = "Soil Match",
                    ParcelAttributePath = "characteristics.soiltype",
                    ExpectedValue = parcel.Characteristics!.SoilType!,
                    Weight = 0.05m
                },
                new CustomCriterionCriteria
                {
                    Key = "district-match",
                    Name = "District Match",
                    ParcelAttributePath = "location.district",
                    ExpectedValue = parcel.Location.District,
                    Weight = 0.05m
                }
            ]
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Single(
            recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria),
            c => c.Key == "custom-criteria");
    }

    [Fact]
    public async Task RecommendAsync_duplicate_evaluator_injection_duplicates_criteria_but_preserves_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var standardEvaluators = RecommendationEngineTestFactory.CreateStandardEvaluators();
        var duplicatedEvaluators = standardEvaluators.Concat(standardEvaluators).ToList();

        var singleEngine = RecommendationEngineTestFactory.CreateWithEvaluators(standardEvaluators, parcel);
        var duplicatedEngine = RecommendationEngineTestFactory.CreateWithEvaluators(duplicatedEvaluators, parcel);

        var request = CreateRequest() with { TargetParcelId = parcel.Id };

        var singleResult = Assert.Single((await singleEngine.RecommendAsync(request)).Recommendations);
        var duplicatedResult = Assert.Single((await duplicatedEngine.RecommendAsync(request)).Recommendations);

        var singleCriteriaCount = singleResult.MatchingCriteria.Count + singleResult.FailedCriteria.Count;
        var duplicatedCriteriaCount = duplicatedResult.MatchingCriteria.Count + duplicatedResult.FailedCriteria.Count;

        Assert.Equal(singleCriteriaCount * 2, duplicatedCriteriaCount);
        Assert.Equal(singleResult.SuitabilityScore, duplicatedResult.SuitabilityScore);
    }

    private static LandRecommendationSearchRequest CreateRoadDistanceFilterRequest(decimal maxRoadDistanceMeters) =>
        new()
        {
            RequiredPurpose = LandUseType.Agricultural,
            Accessibility = new AccessibilityCriteria
            {
                MaxRoadDistanceMeters = maxRoadDistanceMeters
            },
            MaxResults = 5
        };

    private static LandRecommendationSearchRequest CreateAreaFilterRequest(
        decimal requiredAreaHectares,
        decimal areaTolerancePercent = 0m) =>
        new()
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredAreaHectares = requiredAreaHectares,
            AreaTolerancePercent = areaTolerancePercent,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            PreferredLocation = new PreferredLocationCriteria
            {
                Province = "Western"
            },
            MaxResults = 5
        };

    private static LandRecommendationSearchRequest CreateRequest() => new()
    {
        RequiredPurpose = LandUseType.Agricultural,
        RequiredAreaHectares = 5m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        PreferredLocation = new PreferredLocationCriteria
        {
            Province = "Western"
        },
        Accessibility = new AccessibilityCriteria
        {
            RequireRoadAccess = true,
            MaxRoadDistanceMeters = 1000m
        },
        Environmental = new EnvironmentalCriteria(),
        Regulatory = new RegulatoryCriteria()
    };
}

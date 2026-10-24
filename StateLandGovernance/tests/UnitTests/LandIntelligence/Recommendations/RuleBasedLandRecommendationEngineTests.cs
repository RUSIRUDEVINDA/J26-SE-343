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
            RequiredPurpose = LandUseType.Agricultural
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Equal(0m, recommendation.SuitabilityScore);
        Assert.True(recommendation.HardConstraintRejected);
        Assert.NotEmpty(recommendation.HardConstraintReason);
        Assert.NotEmpty(recommendation.FailedCriteria);
        Assert.NotEmpty(recommendation.Restrictions);
        Assert.Contains(recommendation.Restrictions, r => r.Severity >= RestrictionSeverity.High);
        Assert.Contains("rejected", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(recommendation.Evidence, e => e.RelatedCriterionName == "HardConstraintRejection");
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
    public async Task RecommendAsync_includes_parcel_smaller_than_target_with_reduced_area_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(7m, "SYNTH-AREA-070");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(targetAreaHectares: 10m));

        var recommendation = Assert.Single(response.Recommendations);
        var targetArea = Assert.Single(
            recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria),
            c => c.Key == "target-area");
        Assert.Equal(70m, targetArea.Score);
        Assert.Equal(1, response.CandidateCount);
    }

    [Fact]
    public async Task RecommendAsync_scores_target_area_with_continuous_formula()
    {
        var parcel148 = SyntheticRecommendationParcelFactory.CreateParcelWithArea(14.8m, "SYNTH-AREA-148-MATCH");
        var parcel225 = SyntheticRecommendationParcelFactory.CreateParcelWithArea(22.5m, "SYNTH-AREA-225-MATCH");
        var engine = RecommendationEngineTestFactory.Create(parcel148, parcel225);

        var response = await engine.RecommendAsync(CreateAreaFilterRequest(targetAreaHectares: 10m));

        static decimal TargetAreaScore(LandParcelRecommendationResult recommendation) =>
            recommendation.MatchingCriteria
                .Concat(recommendation.FailedCriteria)
                .Single(c => c.Key == "target-area")
                .Score;

        var score148 = TargetAreaScore(
            response.Recommendations.Single(r => r.ParcelId == parcel148.Id));
        var score225 = TargetAreaScore(
            response.Recommendations.Single(r => r.ParcelId == parcel225.Id));

        Assert.Equal(52m, score148);
        Assert.Equal(0m, score225);
        Assert.NotEqual(
            response.Recommendations[0].SuitabilityScore,
            response.Recommendations[1].SuitabilityScore);
    }

    [Fact]
    public async Task RecommendAsync_marks_identical_rule_scores_as_tied_with_shared_rank()
    {
        var parcelA = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-TIE-AAA");
        var parcelB = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-TIE-BBB");
        var engine = RecommendationEngineTestFactory.Create(parcelA, parcelB);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            PreferredLocation = new PreferredLocationCriteria { Province = "Western" },
            MaxResults = 5
        });

        Assert.Equal(2, response.Recommendations.Count);
        Assert.All(response.Recommendations, r =>
        {
            Assert.True(r.IsTiedOnRuleScore);
            Assert.Equal(2, r.RuleScoreTieCount);
            Assert.Equal(1, r.RuleScoreRank);
        });
        Assert.Equal("SYNTH-TIE-AAA", response.Recommendations[0].CadastralNumber);
    }

    [Fact]
    public async Task RecommendAsync_does_not_filter_or_score_by_road_distance()
    {
        var near = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(2000m, "SYNTH-ROAD-NEAR");
        var far = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(12000m, "SYNTH-ROAD-FAR");
        var missing = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(null, "SYNTH-ROAD-MISSING");
        var engine = RecommendationEngineTestFactory.Create(near, far, missing);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 5
        });

        Assert.Equal(3, response.CandidateCount);
        Assert.Equal(3, response.Recommendations.Count);
        Assert.DoesNotContain(
            response.Recommendations.SelectMany(r => r.MatchingCriteria.Concat(r.FailedCriteria)),
            c => c.Key == "accessibility");
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
            MaxResults = 2
        });

        Assert.Equal(2, response.Recommendations.Count);
        Assert.Equal(suitable.Id, response.Recommendations[0].ParcelId);
        Assert.True(response.Recommendations[0].SuitabilityScore > response.Recommendations[1].SuitabilityScore);
        Assert.True(response.Recommendations[1].HardConstraintRejected);
        Assert.Contains("Prohibitive environmental restriction", response.Recommendations[1].HardConstraintReason ?? string.Empty);
    }

    [Fact]
    public async Task RecommendAsync_applies_regulatory_reference_limits_from_persisted_counts()
    {
        var parcelNone = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-REG-RANK-000");
        var parcelTwo = SyntheticRecommendationParcelFactory.CreateParcelWithRegulatoryReferenceCount(
            2,
            "SYNTH-REG-RANK-002");
        var parcelFive = SyntheticRecommendationParcelFactory.CreateParcelWithRegulatoryReferenceCount(
            5,
            "SYNTH-REG-RANK-005");
        var engine = RecommendationEngineTestFactory.Create(parcelNone, parcelTwo, parcelFive);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            PreferredLocation = new PreferredLocationCriteria
            {
                Province = "Western"
            },
            Regulatory = new RegulatoryCriteria
            {
                MaxRegulatoryReferences = 3,
                PenalizeMultipleReferences = true
            },
            MaxResults = 3
        });

        Assert.Equal(3, response.Recommendations.Count);

        var noneResult = response.Recommendations.Single(r => r.ParcelId == parcelNone.Id);
        var twoResult = response.Recommendations.Single(r => r.ParcelId == parcelTwo.Id);
        var fiveResult = response.Recommendations.Single(r => r.ParcelId == parcelFive.Id);

        Assert.Contains(noneResult.MatchingCriteria, c => c.Key == "regulatory");
        Assert.Contains(twoResult.MatchingCriteria, c => c.Key == "regulatory");
        Assert.Contains(fiveResult.FailedCriteria, c => c.Key == "regulatory");

        Assert.True(noneResult.SuitabilityScore >= twoResult.SuitabilityScore);
        Assert.True(twoResult.SuitabilityScore > fiveResult.SuitabilityScore);
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
    public async Task RecommendAsync_returns_unique_provenance_backed_evidence_per_criterion()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id
        });

        var recommendation = Assert.Single(response.Recommendations);
        var provenanceBackedEvidence = recommendation.Evidence
            .Where(e => e.DataProvenance is not null)
            .Select(e => e.RelatedCriterionName)
            .ToList();

        Assert.NotEmpty(provenanceBackedEvidence);
        Assert.Equal(
            provenanceBackedEvidence.Count,
            provenanceBackedEvidence.Distinct(StringComparer.Ordinal).Count());
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

        Assert.Equal(7, evaluatedCriteria.Count);
        Assert.Contains(evaluatedCriteria, c => c.Key == "purpose-alignment");
        Assert.Contains(evaluatedCriteria, c => c.Key == "target-area");
        Assert.DoesNotContain(evaluatedCriteria, c => c.Key is "required-area" or "preferred-area");
        Assert.Contains(evaluatedCriteria, c => c.Key == "land-category");
        Assert.Contains(evaluatedCriteria, c => c.Key == "land-use");
        Assert.Contains(evaluatedCriteria, c => c.Key == "location-preference");
        Assert.Contains(evaluatedCriteria, c => c.Key == "regulatory");
        Assert.Contains(evaluatedCriteria, c => c.Key == "spatial-constraints");
        Assert.DoesNotContain(evaluatedCriteria, c => c.Key is "accessibility" or "water-proximity" or "soil-group" or "environmental");
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

    private static LandRecommendationSearchRequest CreateAreaFilterRequest(decimal targetAreaHectares) =>
        new()
        {
            RequiredPurpose = LandUseType.Agricultural,
            TargetAreaHectares = targetAreaHectares,
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
        TargetAreaHectares = 5m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        PreferredLocation = new PreferredLocationCriteria
        {
            Province = "Western"
        },
        Regulatory = new RegulatoryCriteria()
    };
}

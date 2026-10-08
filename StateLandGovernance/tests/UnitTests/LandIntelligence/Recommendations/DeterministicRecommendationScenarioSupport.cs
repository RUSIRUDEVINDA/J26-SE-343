using System.Diagnostics;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

internal sealed record ScenarioExpectation(
    string CadastralLabel,
    int? ExpectedRank,
    bool ExpectedExcluded,
    string Rationale);

internal sealed record DeterministicScenarioRunResult(
    LandUseType Purpose,
    LandRecommendationSearchResponse Response,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, LandParcelRecommendationResult> ResultsByLabel);

internal static class DeterministicRecommendationScenarioSupport
{
    public static LandRecommendationSearchRequest CreateScenarioRequest(LandUseType purpose) =>
        new()
        {
            RequiredPurpose = purpose,
            RequiredAreaHectares = 5m,
            AreaTolerancePercent = 15m,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = purpose,
            PreferredLocation = new PreferredLocationCriteria
            {
                Province = "Western"
            },
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true,
                MaxRoadDistanceMeters = 5000m
            },
            Environmental = new EnvironmentalCriteria
            {
                MaxAllowedEnvironmentalSeverity = RestrictionSeverity.Medium,
                RejectProhibitiveEnvironmentalRestrictions = true
            },
            Regulatory = new RegulatoryCriteria
            {
                MaxRegulatoryReferences = 2,
                PenalizeMultipleReferences = true
            },
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil-type",
                    Name = "Soil Type",
                    ExpectedValue = "Loam",
                    ParcelAttributePath = "characteristics.soiltype",
                    Weight = 0.05m
                }
            ],
            MaxResults = 10
        };

    public static async Task<DeterministicScenarioRunResult> RunScenarioAsync(
        LandUseType purpose,
        string prefix,
        RuleBasedLandRecommendationEngine engine)
    {
        var request = CreateScenarioRequest(purpose);
        var stopwatch = Stopwatch.StartNew();
        var response = await engine.RecommendAsync(request);
        stopwatch.Stop();

        var resultsByLabel = response.Recommendations
            .Where(result => result.CadastralNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(result => result.CadastralNumber, StringComparer.OrdinalIgnoreCase);

        return new DeterministicScenarioRunResult(purpose, response, stopwatch.Elapsed, resultsByLabel);
    }

    public static void AssertScenarioExpectations(
        DeterministicScenarioRunResult run,
        IReadOnlyList<ScenarioExpectation> expectations)
    {
        foreach (var expectation in expectations.Where(e => e.ExpectedExcluded))
        {
            Assert.DoesNotContain(
                run.Response.Recommendations,
                result => result.CadastralNumber.Equals(expectation.CadastralLabel, StringComparison.OrdinalIgnoreCase));
        }

        var rankedExpectations = expectations
            .Where(expectation => expectation.ExpectedRank is not null)
            .OrderBy(expectation => expectation.ExpectedRank)
            .ToList();

        Assert.Equal(rankedExpectations.Count, run.Response.Recommendations.Count);

        for (var index = 0; index < rankedExpectations.Count; index++)
        {
            var expected = rankedExpectations[index];
            var actual = run.Response.Recommendations[index];

            Assert.Equal(expected.CadastralLabel, actual.CadastralNumber, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(expected.ExpectedRank, actual.Rank);
        }

        Assert.True(
            run.Elapsed < TimeSpan.FromSeconds(2),
            $"Scenario {run.Purpose} exceeded response time threshold: {run.Elapsed.TotalMilliseconds} ms");
    }

    public static void AssertRecommendationArtifacts(LandParcelRecommendationResult result)
    {
        Assert.True(result.SuitabilityScore >= 0m);
        Assert.NotEmpty(result.MatchingCriteria);
        Assert.NotEmpty(result.Evidence);
        Assert.NotEmpty(result.Explanation);

        var totalCriteria = result.MatchingCriteria.Count + result.FailedCriteria.Count;
        Assert.True(totalCriteria >= 8, $"Expected comprehensive criterion coverage, got {totalCriteria}.");
    }

    public static void AssertMonotonicRanking(IReadOnlyList<LandParcelRecommendationResult> recommendations)
    {
        for (var index = 1; index < recommendations.Count; index++)
        {
            Assert.True(
                recommendations[index - 1].SuitabilityScore >= recommendations[index].SuitabilityScore,
                $"Rank {index} ({recommendations[index - 1].CadastralNumber}, {recommendations[index - 1].SuitabilityScore}) " +
                $"should score >= rank {index + 1} ({recommendations[index].CadastralNumber}, {recommendations[index].SuitabilityScore}).");
        }
    }
}

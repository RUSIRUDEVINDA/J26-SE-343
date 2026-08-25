using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class DeterministicRecommendationScenarioTests
{
    public static IEnumerable<object[]> ScenarioCases() =>
    [
        [LandUseType.Agricultural, DeterministicRecommendationScenarioFactory.AgriculturalPrefix],
        [LandUseType.Residential, DeterministicRecommendationScenarioFactory.ResidentialPrefix],
        [LandUseType.Commercial, DeterministicRecommendationScenarioFactory.CommercialPrefix]
    ];

    [Theory]
    [MemberData(nameof(ScenarioCases))]
    public async Task RecommendAsync_ranks_scenario_parcels_in_expected_order(
        LandUseType purpose,
        string prefix)
    {
        var parcels = DeterministicRecommendationScenarioFactory.CreateScenarioParcels(purpose, prefix);
        var engine = RecommendationEngineTestFactory.Create(parcels.ToArray());
        var run = await DeterministicRecommendationScenarioSupport.RunScenarioAsync(purpose, prefix, engine);
        var expectations = DeterministicRecommendationScenarioExpectations.SharedExpectations(prefix);

        DeterministicRecommendationScenarioSupport.AssertScenarioExpectations(run, expectations);
        DeterministicRecommendationScenarioSupport.AssertMonotonicRanking(run.Response.Recommendations);

        Assert.Equal(6, run.Response.Recommendations.Count);
        Assert.Equal(6, run.Response.CandidateCount);
    }

    [Theory]
    [MemberData(nameof(ScenarioCases))]
    public async Task RecommendAsync_captures_explainability_artifacts_for_each_ranked_parcel(
        LandUseType purpose,
        string prefix)
    {
        var parcels = DeterministicRecommendationScenarioFactory.CreateScenarioParcels(purpose, prefix);
        var engine = RecommendationEngineTestFactory.Create(parcels.ToArray());
        var run = await DeterministicRecommendationScenarioSupport.RunScenarioAsync(purpose, prefix, engine);

        foreach (var recommendation in run.Response.Recommendations)
        {
            DeterministicRecommendationScenarioSupport.AssertRecommendationArtifacts(recommendation);
        }

        var high = run.ResultsByLabel[$"{prefix}-A"];
        Assert.Empty(high.FailedCriteria);
        Assert.Contains(high.MatchingCriteria, criterion => criterion.Key == "purpose-alignment");
        Assert.Contains(high.Evidence, evidence => evidence.RelatedCriterionName == "Purpose Alignment");

        var environmental = run.ResultsByLabel[$"{prefix}-D"];
        Assert.Contains(environmental.FailedCriteria, criterion => criterion.Key == "environmental");
        Assert.NotEmpty(environmental.Restrictions);
        Assert.Contains(environmental.Evidence, evidence =>
            evidence.Description.Contains("Environmental", StringComparison.OrdinalIgnoreCase));

        var missingData = run.ResultsByLabel[$"{prefix}-H"];
        Assert.Contains(missingData.FailedCriteria, criterion => criterion.Key == "custom-criteria");
        Assert.Contains(missingData.Evidence, evidence => evidence.RelatedCriterionName == "Custom Criteria");

        var regulatory = run.ResultsByLabel[$"{prefix}-I"];
        Assert.Contains(regulatory.FailedCriteria, criterion => criterion.Key == "regulatory");
        Assert.True(regulatory.Restrictions.Count >= 5);
    }

    [Fact]
    public async Task Agricultural_scenario_high_suitability_outscores_low_suitability_by_margin()
    {
        const string prefix = DeterministicRecommendationScenarioFactory.AgriculturalPrefix;
        var parcels = DeterministicRecommendationScenarioFactory.CreateScenarioParcels(LandUseType.Agricultural, prefix);
        var engine = RecommendationEngineTestFactory.Create(parcels.ToArray());
        var run = await DeterministicRecommendationScenarioSupport.RunScenarioAsync(
            LandUseType.Agricultural,
            prefix,
            engine);

        var high = run.ResultsByLabel[$"{prefix}-A"];
        var low = run.ResultsByLabel[$"{prefix}-C"];
        var environmental = run.ResultsByLabel[$"{prefix}-D"];

        Assert.True(high.SuitabilityScore - low.SuitabilityScore >= 4m);
        Assert.True(low.SuitabilityScore > environmental.SuitabilityScore);
    }

    [Fact]
    public async Task Agricultural_scenario_excluded_parcels_do_not_receive_rank()
    {
        const string prefix = DeterministicRecommendationScenarioFactory.AgriculturalPrefix;
        var parcels = DeterministicRecommendationScenarioFactory.CreateScenarioParcels(LandUseType.Agricultural, prefix);
        var engine = RecommendationEngineTestFactory.Create(parcels.ToArray());
        var run = await DeterministicRecommendationScenarioSupport.RunScenarioAsync(
            LandUseType.Agricultural,
            prefix,
            engine);

        Assert.DoesNotContain(run.Response.Recommendations, result => result.CadastralNumber == $"{prefix}-E");
        Assert.DoesNotContain(run.Response.Recommendations, result => result.CadastralNumber == $"{prefix}-F");
        Assert.DoesNotContain(run.Response.Recommendations, result => result.CadastralNumber == $"{prefix}-G");
    }
}

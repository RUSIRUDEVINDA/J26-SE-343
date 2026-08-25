using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;
using StateLandGovernance.LandIntelligence.Presentation.DependencyInjection;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.UnitTests.LandIntelligence;

public sealed class LandIntelligenceDependencyInjectionTests
{
    private static void EnsureLandIntelligenceConnectionConfigured()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")))
        {
            Environment.SetEnvironmentVariable(
                "LAND_INTELLIGENCE_CONNECTION",
                "Host=localhost;Port=5432;Database=land_intelligence_test;Username=test;Password=test");
        }
    }

    [Fact]
    public void Criterion_evaluators_are_registered_once_when_infrastructure_is_registered_once()
    {
        EnsureLandIntelligenceConnectionConfigured();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(new ConfigurationBuilder().Build());
        services.AddLandIntelligencePresentation();

        using var provider = services.BuildServiceProvider();
        var evaluators = provider.GetServices<IRecommendationCriterionEvaluator>().ToList();

        Assert.Equal(10, evaluators.Count);
        Assert.Equal(
            evaluators.Count,
            evaluators.Select(e => e.GetType()).Distinct().Count());
    }

    [Fact]
    public void Duplicate_infrastructure_registration_registers_duplicate_evaluators()
    {
        EnsureLandIntelligenceConnectionConfigured();

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);
        services.AddLandIntelligenceInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var evaluators = provider.GetServices<IRecommendationCriterionEvaluator>().ToList();

        Assert.Equal(20, evaluators.Count);
        Assert.Equal(2, evaluators.Count(e => e.GetType().Name == nameof(PurposeAlignmentCriterionEvaluator)));
        Assert.Equal(2, evaluators.Count(e => e.GetType().Name == nameof(RequiredAreaCriterionEvaluator)));
    }
}

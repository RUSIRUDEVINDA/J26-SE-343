using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisCoverage;

[Trait("Category", "Integration")]
public sealed class LandIntelligenceGisCoverageIntegrationTests
{
    [Fact]
    public async Task MeasureCoverage_WhenResearchDatabaseConnected_ReportsLiveCounts()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);
        var connection = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);
        services.AddLandIntelligenceApplication();
        await using var provider = services.BuildServiceProvider();

        var db = provider.GetRequiredService<LandIntelligenceDbContext>();
        await db.Database.MigrateAsync();

        var coverageService = provider.GetRequiredService<ILandIntelligenceGisCoverageService>();
        var coverage = await coverageService.MeasureCoverageAsync();

        Assert.True(coverage.TotalParcelCount > 0);
        Assert.NotEmpty(coverage.IndexedDistricts);
        Assert.Equal(coverage.TotalParcelCount, coverage.IndexedDistricts.Sum(d => d.ParcelCount));
        Assert.Equal(6, coverage.IntelligenceLayers.Count);

        if (connection.Contains("land_intel_colombo_state_research", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(300, coverage.TotalParcelCount);
            Assert.Equal(0, coverage.EnrichmentSnapshotCount);
            Assert.Equal(GisIntelligenceCoverageStatus.Unavailable, coverage.OverallEnrichmentStatus);
            Assert.All(
                coverage.IntelligenceLayers,
                layer =>
                {
                    Assert.Equal(GisIntelligenceCoverageStatus.Unavailable, layer.CoverageStatus);
                    Assert.Equal(0, layer.ParcelsWithEvidenceCount);
                });
        }
    }
}

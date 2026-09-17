using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using Xunit.Abstractions;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class H8GisDatabaseCleanupVerificationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private LandIntelligenceDbContext? _dbContext;

    public H8GisDatabaseCleanupVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Database_has_no_synthetic_h8_erosion_records()
    {
        Assert.NotNull(_dbContext);

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);

        var pilotCount = await LandIntelligenceH8GisTestData.ReadPilotErosionObservationCountAsync(_dbContext);
        var syntheticCount = await LandIntelligenceH8GisTestData.ReadSyntheticErosionObservationCountAsync(_dbContext);
        var importedCount = await _dbContext.GisSoilErosionObservations
            .CountAsync(observation => observation.SourceName == GisReferenceDataPaths.SourceName);

        _output.WriteLine($"pilot_erosion_count={pilotCount}");
        _output.WriteLine($"synthetic_h8_count={syntheticCount}");
        _output.WriteLine($"imported_source_count={importedCount}");

        Assert.Equal(0, syntheticCount);
        Assert.Equal(0, pilotCount);
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using Xunit.Abstractions;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class GisReferenceDataImportIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private IGisReferenceDataImportService? _importService;
    private LandIntelligenceDbContext? _dbContext;
    private string? _dataRoot;

    public GisReferenceDataImportIntegrationTests(ITestOutputHelper output)
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
        _importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();
        _dataRoot = GisReferenceDataPaths.ResolveDataRoot(null);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ImportHambantotaPilotAsync_loads_real_geojson_and_is_idempotent()
    {
        Assert.NotNull(_importService);
        Assert.NotNull(_dbContext);
        Assert.NotNull(_dataRoot);

        var first = await _importService.ImportHambantotaPilotAsync(_dataRoot);
        var second = await _importService.ImportHambantotaPilotAsync(_dataRoot);

        Assert.Equal("Hambantota", first.HambantotaDistrictName);
        Assert.Equal("Southern", first.ProvinceName);
        Assert.True(first.TableCounts["gis_administrative_boundaries"] >= 2);
        Assert.Equal(first.TableCounts, second.TableCounts);
        Assert.True(second.FeaturesUpdated >= 1);
        Assert.Equal(0, second.FeaturesImported);

        WriteImportSummary(first);
    }

    private void WriteImportSummary(GisReferenceDataImportResult result)
    {
        _output.WriteLine($"Imported={result.FeaturesImported}, Updated={result.FeaturesUpdated}, Skipped={result.FeaturesSkipped}");
        foreach (var entry in result.TableCounts.OrderBy(pair => pair.Key))
        {
            _output.WriteLine($"{entry.Key}={entry.Value}");
        }

        foreach (var skip in result.SkipLog)
        {
            _output.WriteLine($"SKIP: {skip}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

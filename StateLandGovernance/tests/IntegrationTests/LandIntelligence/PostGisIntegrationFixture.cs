using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

/// <summary>
/// Shared PostgreSQL/PostGIS fixture — seeds synthetic GIS data once per test collection.
/// </summary>
public sealed class PostGisIntegrationFixture : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;

    public ISpatialAnalysisService SpatialAnalysisService { get; private set; } = null!;
    public LandIntelligenceDbContext DbContext { get; private set; } = null!;
    public SyntheticGisDataset Dataset { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        SpatialAnalysisService = _serviceProvider.GetRequiredService<ISpatialAnalysisService>();
        DbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        Dataset = SyntheticGisDataset.Create();
        await Dataset.SeedAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        if (DbContext is not null)
        {
            await Dataset.RemoveAsync(DbContext);
            await DbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

[CollectionDefinition(PostGisIntegrationCollection.Name)]
public sealed class PostGisIntegrationCollection : ICollectionFixture<PostGisIntegrationFixture>
{
    public const string Name = "PostGisIntegration";
}

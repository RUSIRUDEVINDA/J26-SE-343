using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class LandIntelligencePostGisConnectionIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private LandIntelligenceDbContext? _dbContext;

    public Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DbContext_connects_to_state_land_governance_with_postgis_enabled()
    {
        Assert.NotNull(_dbContext);

        var canConnect = await _dbContext.Database.CanConnectAsync();
        Assert.True(
            canConnect,
            "LandIntelligence DbContext could not connect to PostgreSQL. " +
            "Ensure PostgreSQL is running and LAND_INTELLIGENCE_CONNECTION is set in .env at the repository root.");

        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using (var databaseCommand = connection.CreateCommand())
        {
            databaseCommand.CommandText = "SELECT current_database()";
            var databaseName = (string?)await databaseCommand.ExecuteScalarAsync();
            Assert.Equal("state_land_governance", databaseName);
        }

        await using (var postgisCommand = connection.CreateCommand())
        {
            postgisCommand.CommandText = "SELECT PostGIS_Version()";
            var postgisVersion = (string?)await postgisCommand.ExecuteScalarAsync();
            Assert.False(
                string.IsNullOrWhiteSpace(postgisVersion),
                "PostGIS is not available in the connected database. Install the PostGIS extension on state_land_governance.");
        }

        await using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'land_intelligence')";
            var schemaExists = (bool?)await schemaCommand.ExecuteScalarAsync();
            Assert.True(
                schemaExists == true,
                "The land_intelligence schema was not found. Apply EF migrations to state_land_governance.");
        }

        var seededCategoryCount = await _dbContext.LandCategories.CountAsync();
        Assert.True(
            seededCategoryCount >= 4,
            "Expected synthetic land category seed data in land_intelligence.land_categories. Apply EF migrations if the schema is empty.");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}

using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class LandParcelPersistenceIntegrationTests : IAsyncLifetime
{
    private const double ExpectedLatitude = 6.9271;
    private const double ExpectedLongitude = 79.8612;
    private const int ExpectedSpatialReferenceSystemId = 4326;

    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private LandIntelligenceDbContext? _dbContext;
    private Guid _persistedParcelId;

    public Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Can_persist_and_retrieve_synthetic_land_parcel_with_point_geometry()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var cadastralNumber = $"SYNTHETIC-INT-{Guid.NewGuid():N}"[..28];

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-SURVEY-PLAN-001"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Integration test parcel"),
            new LandArea(2.5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS", "GN-Test"),
            new SpatialReference(ExpectedLatitude, ExpectedLongitude, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC] Test agricultural use"),
            new LandCharacteristics("Red Yellow Latosol", "Flat terrain", 12m));

        _persistedParcelId = parcel.Id;

        await _repository.AddAsync(parcel);

        var retrievedById = await _repository.GetByIdAsync(parcel.Id);
        var retrievedByCadastralNumber = await _repository.GetByCadastralNumberAsync(cadastralNumber);

        Assert.NotNull(retrievedById);
        Assert.NotNull(retrievedByCadastralNumber);

        Assert.Equal(parcel.Id, retrievedById.Id);
        Assert.Equal(cadastralNumber, retrievedById.Identifier.CadastralNumber);
        Assert.Equal("SYNTHETIC-SURVEY-PLAN-001", retrievedById.Identifier.SurveyPlanReference);
        Assert.Equal(LandCategoryType.StateLand, retrievedById.Category.Type);
        Assert.Equal(LandUseType.Agricultural, retrievedById.CurrentUse?.Type);
        Assert.Equal(2.5m, retrievedById.Area.Value);
        Assert.Equal(AreaUnit.Hectares, retrievedById.Area.Unit);
        Assert.Equal("Western", retrievedById.Location.Province);
        Assert.Equal("Colombo", retrievedById.Location.District);
        Assert.Equal("Colombo DS", retrievedById.Location.DivisionalSecretariat);
        Assert.Equal("GN-Test", retrievedById.Location.GramaNiladhariDivision);
        Assert.Equal("Red Yellow Latosol", retrievedById.Characteristics?.SoilType);
        Assert.Equal("Flat terrain", retrievedById.Characteristics?.TerrainDescription);
        Assert.Equal(12m, retrievedById.Characteristics?.ElevationMeters);

        Assert.Equal(ExpectedLatitude, retrievedById.Spatial.CentroidLatitude, precision: 6);
        Assert.Equal(ExpectedLongitude, retrievedById.Spatial.CentroidLongitude, precision: 6);
        Assert.Equal("EPSG:4326", retrievedById.Spatial.CoordinateSystem);

        Assert.Equal(retrievedById.Id, retrievedByCadastralNumber.Id);
        Assert.Equal(retrievedById.Identifier.CadastralNumber, retrievedByCadastralNumber.Identifier.CadastralNumber);

        var persistedCentroid = await GetPersistedCentroidAsync(parcel.Id);
        Assert.Equal(ExpectedLongitude, persistedCentroid.Longitude, precision: 6);
        Assert.Equal(ExpectedLatitude, persistedCentroid.Latitude, precision: 6);
        Assert.Equal(ExpectedSpatialReferenceSystemId, persistedCentroid.SpatialReferenceSystemId);
        Assert.Contains("Point", persistedCentroid.GeometryType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Can_persist_and_reload_environmental_restrictions_with_parcel()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var cadastralNumber = $"SYNTHETIC-ENV-{Guid.NewGuid():N}"[..28];

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-ENV-SURVEY-001"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Environmental persistence parcel"),
            new LandArea(3.1m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS", "GN-Env-Test"),
            new SpatialReference(ExpectedLatitude, ExpectedLongitude, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC] Test agricultural use"));

        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.Wetland,
            "[SYNTHETIC] High conservation restriction",
            RestrictionSeverity.High));

        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.WaterBodyBuffer,
            "[SYNTHETIC] Moderate flood-risk restriction",
            RestrictionSeverity.Medium));

        _persistedParcelId = parcel.Id;

        await _repository.AddAsync(parcel);

        var reloaded = await _repository.GetByIdAsync(parcel.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.EnvironmentalRestrictions.Count);
        Assert.Contains(
            reloaded.EnvironmentalRestrictions,
            r => r.Type == EnvironmentalRestrictionType.Wetland && r.Severity == RestrictionSeverity.High);
        Assert.Contains(
            reloaded.EnvironmentalRestrictions,
            r => r.Description.Contains("[SYNTHETIC] Moderate flood-risk restriction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Can_reload_updated_environmental_restriction_values()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var cadastralNumber = $"SYNTHETIC-ENV-UPD-{Guid.NewGuid():N}"[..28];

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-ENV-UPDATE-001"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Environmental update parcel"),
            new LandArea(2.2m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(ExpectedLatitude, ExpectedLongitude, "EPSG:4326"));

        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.ProtectedArea,
            "[SYNTHETIC] Low erosion-risk restriction",
            RestrictionSeverity.Low));

        _persistedParcelId = parcel.Id;
        await _repository.AddAsync(parcel);

        var restrictionId = parcel.EnvironmentalRestrictions.Single().Id;
        var persistedRestriction = await _dbContext.EnvironmentalRestrictions.FindAsync(restrictionId);
        Assert.NotNull(persistedRestriction);

        persistedRestriction.Description = "[SYNTHETIC] Updated high conservation restriction";
        persistedRestriction.Severity = RestrictionSeverity.High;
        persistedRestriction.Type = EnvironmentalRestrictionType.ForestReserve;
        await _dbContext.SaveChangesAsync();

        var reloaded = await _repository.GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);

        var restriction = Assert.Single(reloaded.EnvironmentalRestrictions);
        Assert.Equal(restrictionId, restriction.Id);
        Assert.Equal(EnvironmentalRestrictionType.ForestReserve, restriction.Type);
        Assert.Equal(RestrictionSeverity.High, restriction.Severity);
        Assert.Equal("[SYNTHETIC] Updated high conservation restriction", restriction.Description);
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is not null && _persistedParcelId != Guid.Empty)
        {
            var entity = await _dbContext.LandParcels.FindAsync(_persistedParcelId);
            if (entity is not null)
            {
                _dbContext.LandParcels.Remove(entity);
                await _dbContext.SaveChangesAsync();
            }
        }

        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    private async Task<(double Longitude, double Latitude, int SpatialReferenceSystemId, string GeometryType)> GetPersistedCentroidAsync(
        Guid parcelId)
    {
        var connection = _dbContext!.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    ST_X("Centroid"),
                    ST_Y("Centroid"),
                    ST_SRID("Centroid"),
                    ST_GeometryType("Centroid")
                FROM land_intelligence.land_parcels
                WHERE "Id" = @parcelId
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "parcelId";
            parameter.Value = parcelId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync();
            var hasRow = await reader.ReadAsync();
            Assert.True(hasRow, "Persisted land parcel was not found in PostgreSQL.");

            return (
                reader.GetDouble(0),
                reader.GetDouble(1),
                reader.GetInt32(2),
                reader.GetString(3));
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}

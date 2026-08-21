using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

public sealed class LandParcelCommandHandlerTests
{
    [Fact]
    public async Task CreateLandParcelCommandHandler_creates_valid_parcel()
    {
        var repository = new InMemoryLandParcelRepository();
        var handler = new CreateLandParcelCommandHandler(
            repository,
            new CreateLandParcelCommandValidator());

        var result = await handler.HandleAsync(CreateValidCreateCommand());

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("SYNTH-CREATE-001", result.Identifier.CadastralNumber);
        Assert.Equal(LandCategoryType.StateLand, result.Category.Type);
        Assert.Equal(LandUseType.Agricultural, result.CurrentUse?.Type);
        Assert.Equal(2.5m, result.Area.Value);
    }

    [Fact]
    public async Task CreateLandParcelCommandHandler_throws_for_invalid_parcel()
    {
        var handler = new CreateLandParcelCommandHandler(
            new InMemoryLandParcelRepository(),
            new CreateLandParcelCommandValidator());

        var invalidCommand = CreateValidCreateCommand() with { CadastralNumber = "  " };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(invalidCommand));

        Assert.Contains(exception.Errors, error => error.Contains("Cadastral number", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_updates_existing_parcel()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-UPDATE-001");
        var repository = new InMemoryLandParcelRepository(existing);
        var handler = new UpdateLandParcelCommandHandler(
            repository,
            new UpdateLandParcelCommandValidator());

        var result = await handler.HandleAsync(new UpdateLandParcelCommand(
            existing.Id,
            LandUseType.Commercial,
            "[SYNTHETIC] Updated commercial use",
            "Clay",
            "Undulating terrain",
            25m));

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(LandUseType.Commercial, result.CurrentUse?.Type);
        Assert.Equal("Clay", result.Characteristics?.SoilType);
        Assert.Equal("Undulating terrain", result.Characteristics?.TerrainDescription);
        Assert.Equal(25m, result.Characteristics?.ElevationMeters);
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_throws_for_invalid_update()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-UPDATE-002");
        var handler = new UpdateLandParcelCommandHandler(
            new InMemoryLandParcelRepository(existing),
            new UpdateLandParcelCommandValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new UpdateLandParcelCommand(existing.Id, null, null, null, null, null)));

        Assert.Contains(exception.Errors, error => error.Contains("At least one updatable field", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_throws_when_parcel_not_found()
    {
        var handler = new UpdateLandParcelCommandHandler(
            new InMemoryLandParcelRepository(),
            new UpdateLandParcelCommandValidator());

        var missingId = Guid.NewGuid();

        await Assert.ThrowsAsync<LandParcelNotFoundException>(() =>
            handler.HandleAsync(new UpdateLandParcelCommand(
                missingId,
                LandUseType.Residential,
                "[SYNTHETIC] Residential use",
                null,
                null,
                null)));
    }

    private static CreateLandParcelCommand CreateValidCreateCommand(string cadastralNumber = "SYNTH-CREATE-001") =>
        new(
            cadastralNumber,
            "SYNTH-SURVEY-001",
            LandCategoryType.StateLand,
            "[SYNTHETIC] Test parcel",
            2.5m,
            AreaUnit.Hectares,
            "Western",
            "Colombo",
            "Colombo DS",
            "GN-Test",
            6.9271,
            79.8612,
            "EPSG:4326",
            null,
            LandUseType.Agricultural,
            "[SYNTHETIC] Agricultural use",
            "Red Yellow Latosol",
            "Flat terrain",
            12m);
}

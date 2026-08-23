using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

public sealed class LandParcelExtendedDataTests
{
    [Fact]
    public async Task CreateLandParcelCommandHandler_persists_boundary_polygon_and_child_collections()
    {
        var repository = new InMemoryLandParcelRepository();
        var handler = new CreateLandParcelCommandHandler(
            repository,
            new NoOpLandParcelGraphSynchronizer(),
            new CreateLandParcelCommandValidator());

        var result = await handler.HandleAsync(
            LandParcelCommandTestData.CreateExtendedCreateCommand("SYNTH-FULL-CREATE-001"));

        Assert.NotNull(result.Spatial.BoundaryPolygon);
        Assert.Equal(1, result.SpatialConstraintCount);
        Assert.Equal(1, result.EnvironmentalRestrictionCount);
        Assert.Equal(1, result.InfrastructureFeatureCount);
        Assert.Equal(1, result.RegulatoryReferenceCount);
        Assert.Equal(AttributeProvenanceSourceType.Official, result.Characteristics?.SoilTypeProvenance?.SourceType);

        var persisted = await repository.GetByIdAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.NotNull(persisted!.Spatial.Boundary);
        Assert.Equal(5, persisted.Spatial.Boundary!.ExteriorRing.Count);
        Assert.Single(persisted.SpatialConstraints);
        Assert.NotNull(persisted.SpatialConstraints.First().Geometry);
        Assert.Single(persisted.EnvironmentalRestrictions);
        Assert.Equal(AttributeProvenanceSourceType.Derived, persisted.EnvironmentalRestrictions.First().DataProvenance?.SourceType);
        Assert.Single(persisted.InfrastructureFeatures);
        Assert.NotNull(persisted.InfrastructureFeatures.First().Location);
        Assert.Single(persisted.RegulatoryReferences);
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_replaces_child_collections_without_duplicating()
    {
        var repository = new InMemoryLandParcelRepository();
        var createHandler = new CreateLandParcelCommandHandler(
            repository,
            new NoOpLandParcelGraphSynchronizer(),
            new CreateLandParcelCommandValidator());

        var created = await createHandler.HandleAsync(
            LandParcelCommandTestData.CreateExtendedCreateCommand("SYNTH-FULL-UPDATE-001"));

        var parcel = await repository.GetByIdAsync(created.Id);
        Assert.NotNull(parcel);

        var constraintId = parcel!.SpatialConstraints.First().Id;
        var restrictionId = parcel.EnvironmentalRestrictions.First().Id;
        var featureId = parcel.InfrastructureFeatures.First().Id;
        var referenceId = parcel.RegulatoryReferences.First().Id;

        var updateHandler = new UpdateLandParcelCommandHandler(
            repository,
            new NoOpLandParcelGraphSynchronizer(),
            new UpdateLandParcelCommandValidator());

        var updated = await updateHandler.HandleAsync(new UpdateLandParcelCommand
        {
            LandParcelId = parcel.Id,
            BoundaryPolygon = LandParcelCommandTestData.SampleBoundaryPolygon,
            SpatialConstraints =
            [
                new SpatialConstraintInputDto
                {
                    Id = constraintId,
                    Type = SpatialConstraintType.BufferZone,
                    Description = "[SYNTHETIC] Updated setback",
                    Severity = RestrictionSeverity.High
                }
            ],
            EnvironmentalRestrictions =
            [
                new EnvironmentalRestrictionInputDto
                {
                    Id = restrictionId,
                    Type = EnvironmentalRestrictionType.WaterBodyBuffer,
                    Description = "[SYNTHETIC] Updated flood restriction",
                    Severity = RestrictionSeverity.Prohibitive,
                    DataProvenance = new AttributeProvenanceDto(
                        AttributeProvenanceSourceType.Synthetic,
                        "Synthetic GIS dataset",
                        1m,
                        null,
                        false)
                }
            ],
            InfrastructureFeatures =
            [
                new InfrastructureFeatureInputDto
                {
                    Id = featureId,
                    Type = InfrastructureFeatureType.Railway,
                    Name = "[SYNTHETIC] Railway",
                    DistanceMeters = 1200m
                }
            ],
            RegulatoryReferences =
            [
                new RegulatoryReferenceInputDto
                {
                    Id = referenceId,
                    GazetteNumber = "SYNTH-GZ-UPDATED",
                    Title = "[SYNTHETIC] Updated gazette",
                    EffectiveDate = new DateOnly(2026, 2, 1)
                }
            ],
            Characteristics = new LandCharacteristicsInputDto
            {
                SoilType = "Clay",
                SoilTypeProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Imputed,
                    "Regional model",
                    0.7m,
                    null,
                    false)
            }
        });

        Assert.Equal(1, updated.SpatialConstraintCount);
        Assert.Equal("Clay", updated.Characteristics?.SoilType);
        Assert.Equal(AttributeProvenanceSourceType.Imputed, updated.Characteristics?.SoilTypeProvenance?.SourceType);

        var reloaded = await repository.GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.SpatialConstraints);
        Assert.Equal(constraintId, reloaded.SpatialConstraints.First().Id);
        Assert.Equal(SpatialConstraintType.BufferZone, reloaded.SpatialConstraints.First().Type);
        Assert.Single(reloaded.EnvironmentalRestrictions);
        Assert.Equal(EnvironmentalRestrictionType.WaterBodyBuffer, reloaded.EnvironmentalRestrictions.First().Type);
        Assert.Single(reloaded.InfrastructureFeatures);
        Assert.Equal(InfrastructureFeatureType.Railway, reloaded.InfrastructureFeatures.First().Type);
        Assert.Single(reloaded.RegulatoryReferences);
        Assert.Equal("SYNTH-GZ-UPDATED", reloaded.RegulatoryReferences.First().GazetteNumber);
    }

    [Fact]
    public void GeoJsonGeometryMapper_round_trips_polygon_ring()
    {
        var boundary = GeoJsonGeometryMapper.ToGeoBoundary(LandParcelCommandTestData.SampleBoundaryPolygon);

        Assert.NotNull(boundary);
        var restored = GeoJsonGeometryMapper.ToGeoJson(boundary);

        Assert.NotNull(restored);
        Assert.Equal("Polygon", restored!.Type);
        Assert.NotNull(restored.Coordinates);
        Assert.NotEmpty(restored.Coordinates![0]);
    }
}

using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Persistence;

public sealed class LandParcelPersistenceMapperTests
{
    [Fact]
    public void ToDomain_round_trip_preserves_hectare_area()
    {
        var entity = CreateParcelEntity(14.8m, AreaUnit.Hectares);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.Equal(14.8m, parcel.Area.Value);
        Assert.Equal(AreaUnit.Hectares, parcel.Area.Unit);
        Assert.Equal(14.8m, parcel.Area.ToHectares());
    }

    [Fact]
    public void ToDomain_corrects_legacy_hectare_magnitude_stored_as_square_meters()
    {
        var entity = CreateParcelEntity(14.8m, AreaUnit.SquareMeters);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.Equal(14.8m, parcel.Area.Value);
        Assert.Equal(AreaUnit.Hectares, parcel.Area.Unit);
        Assert.Equal(14.8m, parcel.Area.ToHectares());
    }

    [Fact]
    public void ToDomain_preserves_genuine_square_meter_values_at_or_above_threshold()
    {
        var entity = CreateParcelEntity(5000m, AreaUnit.SquareMeters);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.Equal(5000m, parcel.Area.Value);
        Assert.Equal(AreaUnit.SquareMeters, parcel.Area.Unit);
        Assert.Equal(0.5m, parcel.Area.ToHectares());
    }

    [Fact]
    public void RequiredAreaCriterionEvaluator_passes_for_persisted_14_8_hectare_parcel()
    {
        var parcel = LandParcelPersistenceMapper.ToDomain(CreateParcelEntity(14.8m, AreaUnit.SquareMeters));

        var evaluation = new RequiredAreaCriterionEvaluator().Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                RequiredAreaHectares = 10m,
                AreaTolerancePercent = 15m
            });

        Assert.True(evaluation.IsMet);
        Assert.True(evaluation.Score > 0);
        Assert.Contains("14.80 ha", evaluation.Summary, StringComparison.Ordinal);
    }

    private static LandParcelEntity CreateParcelEntity(decimal areaValue, AreaUnit areaUnit)
    {
        var category = new LandCategoryEntity
        {
            Id = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001"),
            Type = LandCategoryType.StateLand,
            Name = "State Land",
            Description = "[SYNTHETIC]"
        };

        return new LandParcelEntity
        {
            Id = Guid.NewGuid(),
            CadastralNumber = "SYNTH-AREA-001",
            LandCategoryId = category.Id,
            LandCategory = category,
            AreaValue = areaValue,
            AreaUnit = areaUnit,
            Province = "North Central Province",
            District = "Anuradhapura",
            DivisionalSecretariat = "Anuradhapura DS",
            Centroid = NetTopologySuite.NtsGeometryServices.Instance
                .CreateGeometryFactory(4326)
                .CreatePoint(new NetTopologySuite.Geometries.Coordinate(80.4037, 8.3114))
        };
    }
}

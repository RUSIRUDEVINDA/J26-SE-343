using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Persistence;

public sealed class EnvironmentalRestrictionPersistenceMapperTests
{
    [Fact]
    public void ToDomainEnvironmentalRestriction_preserves_supported_fields()
    {
        var entity = new EnvironmentalRestrictionEntity
        {
            Id = Guid.Parse("22222222-0001-4000-8000-000000000001"),
            LandParcelId = Guid.Parse("11111111-0001-4000-8000-000000000001"),
            Type = EnvironmentalRestrictionType.WaterBodyBuffer,
            Description = "[SYNTHETIC] Moderate flood-risk restriction",
            Severity = RestrictionSeverity.Medium
        };

        var restriction = LandParcelPersistenceMapper.ToDomainEnvironmentalRestriction(entity);

        Assert.Equal(entity.Id, restriction.Id);
        Assert.Equal(EnvironmentalRestrictionType.WaterBodyBuffer, restriction.Type);
        Assert.Equal("[SYNTHETIC] Moderate flood-risk restriction", restriction.Description);
        Assert.Equal(RestrictionSeverity.Medium, restriction.Severity);
    }

    [Fact]
    public void ToDomain_populates_environmental_restrictions_on_parcel()
    {
        var parcelId = Guid.Parse("11111111-0001-4000-8000-000000000002");
        var entity = CreateParcelEntity(
            parcelId,
            [
                new EnvironmentalRestrictionEntity
                {
                    Id = Guid.Parse("22222222-0001-4000-8000-000000000010"),
                    LandParcelId = parcelId,
                    Type = EnvironmentalRestrictionType.Wetland,
                    Description = "[SYNTHETIC] High conservation restriction",
                    Severity = RestrictionSeverity.High
                },
                new EnvironmentalRestrictionEntity
                {
                    Id = Guid.Parse("22222222-0001-4000-8000-000000000011"),
                    LandParcelId = parcelId,
                    Type = EnvironmentalRestrictionType.ProtectedArea,
                    Description = "[SYNTHETIC] Low erosion-risk restriction",
                    Severity = RestrictionSeverity.Low
                }
            ]);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.Equal(2, parcel.EnvironmentalRestrictions.Count);
        Assert.Contains(parcel.EnvironmentalRestrictions, r => r.Type == EnvironmentalRestrictionType.Wetland);
        Assert.Contains(parcel.EnvironmentalRestrictions, r => r.Severity == RestrictionSeverity.Low);
    }

    [Fact]
    public void ToDomain_returns_empty_environmental_restrictions_collection_when_none_persisted()
    {
        var entity = CreateParcelEntity(Guid.NewGuid(), []);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.NotNull(parcel.EnvironmentalRestrictions);
        Assert.Empty(parcel.EnvironmentalRestrictions);
    }

    [Fact]
    public void EnvironmentalCriterionEvaluator_evaluates_persisted_high_severity_restriction()
    {
        var parcel = CreateParcelWithRestriction(
            EnvironmentalRestrictionType.ForestReserve,
            "[SYNTHETIC] High conservation restriction",
            RestrictionSeverity.High);

        var evaluation = new EnvironmentalCriterionEvaluator().Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                Environmental = new EnvironmentalCriteria
                {
                    MaxAllowedEnvironmentalSeverity = RestrictionSeverity.Medium,
                    RejectProhibitiveEnvironmentalRestrictions = true
                }
            });

        Assert.False(evaluation.IsMet);
        Assert.Contains("exceeds allowed limit", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("High", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static LandParcelEntity CreateParcelEntity(
        Guid parcelId,
        IReadOnlyList<EnvironmentalRestrictionEntity> restrictions)
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
            Id = parcelId,
            CadastralNumber = "SYNTH-ENV-001",
            LandCategoryId = category.Id,
            LandCategory = category,
            AreaValue = 3m,
            AreaUnit = AreaUnit.Hectares,
            Province = "Western",
            District = "Colombo",
            DivisionalSecretariat = "Colombo DS",
            Centroid = NetTopologySuite.NtsGeometryServices.Instance
                .CreateGeometryFactory(4326)
                .CreatePoint(new NetTopologySuite.Geometries.Coordinate(79.8612, 6.9271)),
            EnvironmentalRestrictions = restrictions.ToList()
        };
    }

    private static LandParcel CreateParcelWithRestriction(
        EnvironmentalRestrictionType type,
        string description,
        RestrictionSeverity severity)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier("SYNTH-ENV-EVAL-001", "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(4m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"));

        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(type, description, severity));
        return parcel;
    }
}

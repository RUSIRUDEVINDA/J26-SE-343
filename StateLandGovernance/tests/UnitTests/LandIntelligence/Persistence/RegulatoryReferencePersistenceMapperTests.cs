using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Persistence;

public sealed class RegulatoryReferencePersistenceMapperTests
{
    [Fact]
    public void ToDomainRegulatoryReference_preserves_supported_fields()
    {
        var entity = new RegulatoryReferenceEntity
        {
            Id = Guid.Parse("33333333-0001-4000-8000-000000000001"),
            LandParcelId = Guid.Parse("11111111-0001-4000-8000-000000000001"),
            GazetteNumber = "SYNTH-GZ-001",
            Title = "[SYNTHETIC] Regulatory reference A",
            EffectiveDate = new DateOnly(2026, 1, 15),
            Summary = "[SYNTHETIC] Summary text"
        };

        var reference = LandParcelPersistenceMapper.ToDomainRegulatoryReference(entity);

        Assert.Equal(entity.Id, reference.Id);
        Assert.Equal("SYNTH-GZ-001", reference.GazetteNumber);
        Assert.Equal("[SYNTHETIC] Regulatory reference A", reference.Title);
        Assert.Equal(new DateOnly(2026, 1, 15), reference.EffectiveDate);
        Assert.Equal("[SYNTHETIC] Summary text", reference.Summary);
    }

    [Fact]
    public void ToPersistenceRegulatoryReference_preserves_supported_fields()
    {
        var parcelId = Guid.Parse("11111111-0001-4000-8000-000000000002");
        var reference = PersistenceEntityIdHelper.SetEntityId(
            new RegulatoryReference(
                "SYNTH-GZ-002",
                "[SYNTHETIC] Regulatory reference B",
                new DateOnly(2026, 2, 1),
                "[SYNTHETIC] Optional summary"),
            Guid.Parse("33333333-0001-4000-8000-000000000002"));

        var parcel = new LandParcel(
            new ParcelIdentifier("SYNTH-REG-MAP-001", "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(2m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"));

        parcel.AddRegulatoryReference(reference);
        PersistenceEntityIdHelper.SetEntityId(parcel, parcelId);

        var entity = LandParcelPersistenceMapper.ToPersistence(parcel).RegulatoryReferences.Single();

        Assert.Equal(reference.Id, entity.Id);
        Assert.Equal(parcelId, entity.LandParcelId);
        Assert.Equal("SYNTH-GZ-002", entity.GazetteNumber);
        Assert.Equal("[SYNTHETIC] Regulatory reference B", entity.Title);
        Assert.Equal(new DateOnly(2026, 2, 1), entity.EffectiveDate);
        Assert.Equal("[SYNTHETIC] Optional summary", entity.Summary);
    }

    [Fact]
    public void ToDomain_populates_regulatory_references_on_parcel()
    {
        var parcelId = Guid.Parse("11111111-0001-4000-8000-000000000003");
        var entity = CreateParcelEntity(
            parcelId,
            [
                new RegulatoryReferenceEntity
                {
                    Id = Guid.Parse("33333333-0001-4000-8000-000000000010"),
                    LandParcelId = parcelId,
                    GazetteNumber = "SYNTH-GZ-010",
                    Title = "[SYNTHETIC] Regulatory reference 1",
                    EffectiveDate = new DateOnly(2026, 1, 1)
                },
                new RegulatoryReferenceEntity
                {
                    Id = Guid.Parse("33333333-0001-4000-8000-000000000011"),
                    LandParcelId = parcelId,
                    GazetteNumber = "SYNTH-GZ-011",
                    Title = "[SYNTHETIC] Regulatory reference 2",
                    EffectiveDate = new DateOnly(2026, 1, 2),
                    Summary = "[SYNTHETIC] Summary 2"
                }
            ]);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.Equal(2, parcel.RegulatoryReferences.Count);
        Assert.Contains(parcel.RegulatoryReferences, r => r.GazetteNumber == "SYNTH-GZ-010");
        Assert.Contains(parcel.RegulatoryReferences, r => r.Summary == "[SYNTHETIC] Summary 2");
    }

    [Fact]
    public void ToDomain_returns_empty_regulatory_references_collection_when_none_persisted()
    {
        var entity = CreateParcelEntity(Guid.NewGuid(), []);

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);

        Assert.NotNull(parcel.RegulatoryReferences);
        Assert.Empty(parcel.RegulatoryReferences);
    }

    [Fact]
    public void RegulatoryCriterionEvaluator_evaluates_persisted_reference_count_within_limit()
    {
        var parcel = CreateParcelWithReferences(2);

        var evaluation = new RegulatoryCriterionEvaluator().Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                Regulatory = new RegulatoryCriteria
                {
                    MaxRegulatoryReferences = 3,
                    PenalizeMultipleReferences = true
                }
            });

        Assert.True(evaluation.IsMet);
        Assert.Equal(90m, evaluation.Score);
        Assert.Contains("2 regulatory reference(s)", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegulatoryCriterionEvaluator_penalizes_persisted_reference_count_above_limit()
    {
        var parcel = CreateParcelWithReferences(5);

        var evaluation = new RegulatoryCriterionEvaluator().Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                Regulatory = new RegulatoryCriteria
                {
                    MaxRegulatoryReferences = 3,
                    PenalizeMultipleReferences = true
                }
            });

        Assert.False(evaluation.IsMet);
        Assert.Equal(40m, evaluation.Score);
        Assert.Contains("5 regulatory references exceed configured limit (3)", evaluation.Summary, StringComparison.Ordinal);
    }

    private static LandParcelEntity CreateParcelEntity(
        Guid parcelId,
        IReadOnlyList<RegulatoryReferenceEntity> references)
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
            CadastralNumber = "SYNTH-REG-001",
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
            RegulatoryReferences = references.ToList()
        };
    }

    private static LandParcel CreateParcelWithReferences(int count)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier("SYNTH-REG-EVAL-001", "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(4m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"));

        for (var index = 1; index <= count; index++)
        {
            parcel.AddRegulatoryReference(new RegulatoryReference(
                $"SYNTH-GZ-{index:000}",
                $"[SYNTHETIC] Regulatory reference {index}",
                new DateOnly(2026, 1, index)));
        }

        return parcel;
    }
}

using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

internal static class LandParcelGisEnrichmentPersistenceMapper
{
    internal static LandParcelGisEnrichmentSnapshotEntity ToSnapshotEntity(LandParcelGisEnrichmentResult result) =>
        new()
        {
            Id = GisDerivedIntelligenceIdentity.CreateSnapshotId(result.ParcelId),
            LandParcelId = result.ParcelId,
            OverallStatus = result.OverallStatus,
            AdministrativeStatus = result.Administrative?.Status,
            DetectedProvince = result.Administrative?.DetectedProvince,
            DetectedDistrict = result.Administrative?.DetectedDistrict,
            ProvinceMatches = result.Administrative?.ProvinceMatches,
            DistrictMatches = result.Administrative?.DistrictMatches,
            GeometryBasis = result.GeometryBasis,
            SourceName = GisReferenceDataPaths.SourceName,
            EnrichedAt = result.CompletedAt
        };

    internal static InfrastructureFeatureEntity? ToRoadInfrastructureEntity(
        LandParcelGisEnrichmentResult result)
    {
        var road = result.RoadAccessibility;
        if (road?.Status != RoadAccessibilityEnrichmentStatus.Available || road.RoadId is null)
        {
            return null;
        }

        return new InfrastructureFeatureEntity
        {
            Id = GisDerivedIntelligenceIdentity.CreateRoadInfrastructureId(result.ParcelId, road.RoadId.Value),
            LandParcelId = result.ParcelId,
            Type = InfrastructureFeatureType.Road,
            Name = road.RoadName ?? "(unnamed mapped road)",
            DistanceMeters = road.DistanceMeters is null ? null : Convert.ToDecimal(road.DistanceMeters.Value),
            Description =
                $"[GIS-DERIVED] Nearest mapped {road.RoadType} from layer '{road.SourceLayer}'.",
            DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                CreateDerivedDistanceProvenance(road.DistanceProvenance, result.CompletedAt))
        };
    }

    internal static InfrastructureFeatureEntity? ToWaterInfrastructureEntity(
        LandParcelGisEnrichmentResult result)
    {
        var water = result.WaterProximity;
        if (water?.Status != WaterProximityEnrichmentStatus.Available || water.FeatureId is null)
        {
            return null;
        }

        return new InfrastructureFeatureEntity
        {
            Id = GisDerivedIntelligenceIdentity.CreateWaterInfrastructureId(result.ParcelId, water.FeatureId.Value),
            LandParcelId = result.ParcelId,
            Type = InfrastructureFeatureType.Other,
            Name = water.FeatureName ?? "(unnamed mapped water feature)",
            DistanceMeters = water.DistanceMeters is null ? null : Convert.ToDecimal(water.DistanceMeters.Value),
            Description =
                $"[GIS-DERIVED] Natural water proximity ({water.FeatureType}) from layer '{water.SourceLayer}'. " +
                "This is not utility water supply.",
            DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                CreateDerivedDistanceProvenance(water.DistanceProvenance, result.CompletedAt))
        };
    }

    internal static ParcelDerivedSoilGroupEntity? ToDerivedSoilGroupEntity(LandParcelGisEnrichmentResult result)
    {
        var soil = result.Soil;
        if (soil?.Status != SoilGroupEnrichmentStatus.Available || soil.PrimarySoilGroupId is null)
        {
            return null;
        }

        var primaryOverlap = soil.Overlaps.FirstOrDefault(item => item.SoilGroupId == soil.PrimarySoilGroupId);

        return new ParcelDerivedSoilGroupEntity
        {
            Id = GisDerivedIntelligenceIdentity.CreateSoilGroupRecordId(result.ParcelId),
            LandParcelId = result.ParcelId,
            SoilGroupReferenceId = soil.PrimarySoilGroupId.Value,
            SoilGroupName = soil.PrimarySoilGroup ?? primaryOverlap?.SoilGroupName ?? "(unknown soil group)",
            OverlapAreaSquareMeters = primaryOverlap?.OverlapAreaSquareMeters is null
                ? null
                : Convert.ToDecimal(primaryOverlap.OverlapAreaSquareMeters),
            OverlapPercentage = soil.OverlapPercentage ?? primaryOverlap?.OverlapPercentage,
            GeometryBasis = soil.GeometryBasis,
            SourceName = soil.SourceName,
            SourceLayer = soil.SourceLayer,
            ProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                CreateDerivedSoilProvenance(soil, result.CompletedAt))!,
            DerivedAt = result.CompletedAt
        };
    }

    internal static IReadOnlyList<EnvironmentalRestrictionEntity> ToConservationRestrictionEntities(
        LandParcelGisEnrichmentResult result)
    {
        var environmental = result.Environmental;
        if (environmental?.Status != EnvironmentalSpatialConstraintEnrichmentStatus.Available
            || environmental.ConservationAreas.Count == 0)
        {
            return [];
        }

        return environmental.ConservationAreas
            .Select(area => new EnvironmentalRestrictionEntity
            {
                Id = GisDerivedIntelligenceIdentity.CreateConservationRestrictionId(result.ParcelId, area.Id),
                LandParcelId = result.ParcelId,
                Type = EnvironmentalRestrictionType.ProtectedArea,
                Description =
                    $"[GIS-DERIVED] Intersects soil conservation area '{area.Name}'" +
                    (area.OverlapPercentage is null
                        ? "."
                        : $" ({area.OverlapPercentage:F2}% overlap)."),
                Severity = RestrictionSeverity.Low,
                DataProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                    CreateDerivedConservationProvenance(environmental, result.CompletedAt, area.SourceLayer))
            })
            .ToList();
    }

    internal static bool IsGisDerivedInfrastructure(InfrastructureFeatureEntity feature) =>
        IsGisDerivedProvenance(AttributeProvenancePersistenceMapper.Deserialize(feature.DistanceProvenanceJson));

    internal static bool IsGisDerivedEnvironmentalRestriction(EnvironmentalRestrictionEntity restriction) =>
        IsGisDerivedProvenance(AttributeProvenancePersistenceMapper.Deserialize(restriction.DataProvenanceJson));

    private static AttributeProvenance CreateDerivedDistanceProvenance(
        AttributeProvenanceDto? sourceProvenance,
        DateTimeOffset derivedAt)
    {
        var mapped = AttributeProvenanceMapper.ToDomain(sourceProvenance);
        return new AttributeProvenance(
            AttributeProvenanceSourceType.Derived,
            GisDerivedIntelligenceOwnership.SourceName,
            mapped?.Confidence ?? 1m,
            derivedAt,
            mapped?.Verified ?? false);
    }

    private static AttributeProvenance CreateDerivedSoilProvenance(
        SoilGroupEnrichmentResult soil,
        DateTimeOffset derivedAt)
    {
        var mapped = AttributeProvenanceMapper.ToDomain(soil.DerivedSoilGroupProvenance);
        return new AttributeProvenance(
            AttributeProvenanceSourceType.Derived,
            GisDerivedIntelligenceOwnership.SourceName,
            mapped?.Confidence ?? 1m,
            derivedAt,
            mapped?.Verified ?? false);
    }

    private static AttributeProvenance CreateDerivedConservationProvenance(
        EnvironmentalSpatialConstraintEnrichmentResult environmental,
        DateTimeOffset derivedAt,
        string sourceLayer)
    {
        var mapped = AttributeProvenanceMapper.ToDomain(environmental.DerivedConservationProvenance);
        return new AttributeProvenance(
            AttributeProvenanceSourceType.Derived,
            GisDerivedIntelligenceOwnership.SourceName,
            mapped?.Confidence ?? 1m,
            derivedAt,
            mapped?.Verified ?? false);
    }

    private static bool IsGisDerivedProvenance(AttributeProvenance? provenance) =>
        provenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName;
}

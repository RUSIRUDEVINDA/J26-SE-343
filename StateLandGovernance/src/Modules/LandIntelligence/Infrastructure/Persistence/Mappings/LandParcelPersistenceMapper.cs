using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

internal static class LandParcelPersistenceMapper
{
    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    public static LandParcelEntity ToPersistence(LandParcel parcel)
    {
        var centroid = CreatePoint(parcel.Spatial.CentroidLongitude, parcel.Spatial.CentroidLatitude);

        return new LandParcelEntity
        {
            Id = parcel.Id,
            CadastralNumber = parcel.Identifier.CadastralNumber,
            SurveyPlanReference = parcel.Identifier.SurveyPlanReference,
            LandCategoryId = LandIntelligenceSeedData.GetCategoryId(parcel.Category.Type),
            CurrentLandUseId = parcel.CurrentUse is null
                ? null
                : LandIntelligenceSeedData.GetLandUseId(parcel.CurrentUse.Type),
            AreaValue = parcel.Area.Value,
            AreaUnit = parcel.Area.Unit,
            Province = parcel.Location.Province,
            District = parcel.Location.District,
            DivisionalSecretariat = parcel.Location.DivisionalSecretariat,
            GramaNiladhariDivision = parcel.Location.GramaNiladhariDivision,
            Centroid = centroid,
            Boundary = PostGisGeometryFactory.ToMultiPolygon(parcel.Spatial.Boundary),
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId,
            SoilType = parcel.Characteristics?.SoilType,
            TerrainDescription = parcel.Characteristics?.TerrainDescription,
            ElevationMeters = parcel.Characteristics?.ElevationMeters,
            CharacteristicsProvenanceJson = AttributeProvenancePersistenceMapper.SerializeCharacteristicsProvenance(
                parcel.Characteristics),
            SpatialConstraints = parcel.SpatialConstraints
                .Select(constraint => ToPersistenceConstraint(constraint, parcel.Id))
                .ToList(),
            InfrastructureFeatures = parcel.InfrastructureFeatures
                .Select(feature => ToPersistenceFeature(feature, parcel.Id))
                .ToList(),
            EnvironmentalRestrictions = parcel.EnvironmentalRestrictions
                .Select(restriction => ToPersistenceEnvironmentalRestriction(restriction, parcel.Id))
                .ToList(),
            RegulatoryReferences = parcel.RegulatoryReferences
                .Select(reference => ToPersistenceRegulatoryReference(reference, parcel.Id))
                .ToList()
        };
    }

    public static LandParcel ToDomain(LandParcelEntity entity)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier(entity.CadastralNumber, entity.SurveyPlanReference),
            new LandCategory(entity.LandCategory.Type, entity.LandCategory.Description),
            new LandArea(entity.AreaValue, ResolvePersistedAreaUnit(entity.AreaValue, entity.AreaUnit)),
            new AdministrativeLocation(
                entity.Province,
                entity.District,
                entity.DivisionalSecretariat,
                entity.GramaNiladhariDivision),
            ToSpatialReference(entity),
            entity.CurrentLandUse is null
                ? null
                : new LandUse(entity.CurrentLandUse.Type, entity.CurrentLandUse.Description),
            entity.SoilType is null && entity.TerrainDescription is null && entity.ElevationMeters is null
                ? null
                : BuildCharacteristics(entity));

        PersistenceEntityIdHelper.SetEntityId(parcel, entity.Id);

        foreach (var constraint in entity.SpatialConstraints)
        {
            var domainConstraint = new SpatialConstraint(
                constraint.Type,
                constraint.Description,
                constraint.Severity,
                PostGisGeometryFactory.ToGeoBoundary(constraint.ConstraintGeometry),
                constraint.Id);
            parcel.AddSpatialConstraint(domainConstraint);
        }

        foreach (var feature in entity.InfrastructureFeatures)
        {
            var domainFeature = new InfrastructureFeature(
                feature.Type,
                feature.Name,
                feature.DistanceMeters,
                feature.Description,
                AttributeProvenancePersistenceMapper.Deserialize(feature.DistanceProvenanceJson),
                PostGisGeometryFactory.ToGeoCoordinate(feature.Location),
                feature.Id);
            parcel.AddInfrastructureFeature(domainFeature);
        }

        foreach (var restriction in entity.EnvironmentalRestrictions)
        {
            parcel.AddEnvironmentalRestriction(ToDomainEnvironmentalRestriction(restriction));
        }

        foreach (var reference in entity.RegulatoryReferences)
        {
            parcel.AddRegulatoryReference(ToDomainRegulatoryReference(reference));
        }

        var gisDerivedIntelligence = ParcelGisDerivedIntelligencePersistenceMapper.ToDomain(
            entity.GisEnrichmentSnapshot,
            entity.DerivedSoilGroup);
        if (gisDerivedIntelligence is not null)
        {
            parcel.AttachGisDerivedIntelligence(gisDerivedIntelligence);
        }

        return parcel;
    }

    public static EnvironmentalRestriction ToDomainEnvironmentalRestriction(EnvironmentalRestrictionEntity entity) =>
        PersistenceEntityIdHelper.SetEntityId(
            new EnvironmentalRestriction(
                entity.Type,
                entity.Description,
                entity.Severity,
                AttributeProvenancePersistenceMapper.Deserialize(entity.DataProvenanceJson)),
            entity.Id);

    public static RegulatoryReference ToDomainRegulatoryReference(RegulatoryReferenceEntity entity) =>
        PersistenceEntityIdHelper.SetEntityId(
            new RegulatoryReference(
                entity.GazetteNumber,
                entity.Title,
                entity.EffectiveDate,
                entity.Summary,
                AttributeProvenancePersistenceMapper.Deserialize(entity.DataProvenanceJson)),
            entity.Id);

    public static SpatialConstraint ToDomain(SpatialConstraintEntity entity) =>
        PersistenceEntityIdHelper.SetEntityId(
            new SpatialConstraint(entity.Type, entity.Description, entity.Severity),
            entity.Id);

    public static void ApplyFullUpdate(LandParcelEntity entity, LandParcel parcel)
    {
        entity.CurrentLandUseId = parcel.CurrentUse is null
            ? null
            : LandIntelligenceSeedData.GetLandUseId(parcel.CurrentUse.Type);
        entity.SoilType = parcel.Characteristics?.SoilType;
        entity.TerrainDescription = parcel.Characteristics?.TerrainDescription;
        entity.ElevationMeters = parcel.Characteristics?.ElevationMeters;
        entity.CharacteristicsProvenanceJson = AttributeProvenancePersistenceMapper.SerializeCharacteristicsProvenance(
            parcel.Characteristics);
        entity.Boundary = PostGisGeometryFactory.ToMultiPolygon(parcel.Spatial.Boundary);

        SyncSpatialConstraints(entity, parcel);
        SyncInfrastructureFeatures(entity, parcel);
        SyncEnvironmentalRestrictions(entity, parcel);
        SyncRegulatoryReferences(entity, parcel);
    }

    public static void ApplyUpdates(LandParcelEntity entity, LandParcel parcel) =>
        ApplyFullUpdate(entity, parcel);

    private static LandCharacteristics BuildCharacteristics(LandParcelEntity entity)
    {
        var (soil, terrain, elevation) = AttributeProvenancePersistenceMapper.DeserializeCharacteristicsProvenance(
            entity.CharacteristicsProvenanceJson);

        return new LandCharacteristics(
            entity.SoilType,
            entity.TerrainDescription,
            entity.ElevationMeters,
            soil,
            terrain,
            elevation);
    }

    private static SpatialReference ToSpatialReference(LandParcelEntity entity)
    {
        var coordinateSystem = $"EPSG:{entity.SpatialReferenceSystemId}";
        return new SpatialReference(
            entity.Centroid.Y,
            entity.Centroid.X,
            coordinateSystem,
            boundaryReference: null,
            PostGisGeometryFactory.ToGeoBoundary(entity.Boundary));
    }

    private static SpatialConstraintEntity ToPersistenceConstraint(SpatialConstraint constraint, Guid parcelId) =>
        new()
        {
            Id = constraint.Id,
            LandParcelId = parcelId,
            Type = constraint.Type,
            Description = constraint.Description,
            Severity = constraint.Severity,
            ConstraintGeometry = PostGisGeometryFactory.ToMultiPolygon(constraint.Geometry),
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId
        };

    private static InfrastructureFeatureEntity ToPersistenceFeature(InfrastructureFeature feature, Guid parcelId) =>
        new()
        {
            Id = feature.Id,
            LandParcelId = parcelId,
            Type = feature.Type,
            Name = feature.Name,
            DistanceMeters = feature.DistanceMeters,
            Description = feature.Description,
            Location = PostGisGeometryFactory.ToPoint(feature.Location),
            DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(feature.DistanceProvenance),
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId
        };

    private static EnvironmentalRestrictionEntity ToPersistenceEnvironmentalRestriction(
        EnvironmentalRestriction restriction,
        Guid parcelId) =>
        new()
        {
            Id = restriction.Id,
            LandParcelId = parcelId,
            Type = restriction.Type,
            Description = restriction.Description,
            Severity = restriction.Severity,
            DataProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(restriction.DataProvenance)
        };

    private static RegulatoryReferenceEntity ToPersistenceRegulatoryReference(
        RegulatoryReference reference,
        Guid parcelId) =>
        new()
        {
            Id = reference.Id,
            LandParcelId = parcelId,
            GazetteNumber = reference.GazetteNumber,
            Title = reference.Title,
            EffectiveDate = reference.EffectiveDate,
            Summary = reference.Summary,
            DataProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(reference.DataProvenance)
        };

    private static void SyncSpatialConstraints(LandParcelEntity entity, LandParcel parcel)
    {
        var desired = parcel.SpatialConstraints.ToList();
        var desiredIds = desired.Select(item => item.Id).ToHashSet();
        foreach (var orphan in entity.SpatialConstraints.Where(item => !desiredIds.Contains(item.Id)).ToList())
        {
            entity.SpatialConstraints.Remove(orphan);
        }

        foreach (var domain in desired)
        {
            var existing = entity.SpatialConstraints.FirstOrDefault(item => item.Id == domain.Id);
            if (existing is null)
            {
                entity.SpatialConstraints.Add(ToPersistenceConstraint(domain, entity.Id));
                continue;
            }

            existing.Type = domain.Type;
            existing.Description = domain.Description;
            existing.Severity = domain.Severity;
            existing.ConstraintGeometry = PostGisGeometryFactory.ToMultiPolygon(domain.Geometry);
        }
    }

    private static void SyncInfrastructureFeatures(LandParcelEntity entity, LandParcel parcel)
    {
        var desired = parcel.InfrastructureFeatures.ToList();
        var desiredIds = desired.Select(item => item.Id).ToHashSet();
        foreach (var orphan in entity.InfrastructureFeatures.Where(item => !desiredIds.Contains(item.Id)).ToList())
        {
            entity.InfrastructureFeatures.Remove(orphan);
        }

        foreach (var domain in desired)
        {
            var existing = entity.InfrastructureFeatures.FirstOrDefault(item => item.Id == domain.Id);
            if (existing is null)
            {
                entity.InfrastructureFeatures.Add(ToPersistenceFeature(domain, entity.Id));
                continue;
            }

            existing.Type = domain.Type;
            existing.Name = domain.Name;
            existing.DistanceMeters = domain.DistanceMeters;
            existing.Description = domain.Description;
            existing.Location = PostGisGeometryFactory.ToPoint(domain.Location);
            existing.DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(domain.DistanceProvenance);
        }
    }

    private static void SyncEnvironmentalRestrictions(LandParcelEntity entity, LandParcel parcel)
    {
        var desired = parcel.EnvironmentalRestrictions.ToList();
        var desiredIds = desired.Select(item => item.Id).ToHashSet();
        foreach (var orphan in entity.EnvironmentalRestrictions.Where(item => !desiredIds.Contains(item.Id)).ToList())
        {
            entity.EnvironmentalRestrictions.Remove(orphan);
        }

        foreach (var domain in desired)
        {
            var existing = entity.EnvironmentalRestrictions.FirstOrDefault(item => item.Id == domain.Id);
            if (existing is null)
            {
                entity.EnvironmentalRestrictions.Add(ToPersistenceEnvironmentalRestriction(domain, entity.Id));
                continue;
            }

            existing.Type = domain.Type;
            existing.Description = domain.Description;
            existing.Severity = domain.Severity;
            existing.DataProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(domain.DataProvenance);
        }
    }

    private static void SyncRegulatoryReferences(LandParcelEntity entity, LandParcel parcel)
    {
        var desired = parcel.RegulatoryReferences.ToList();
        var desiredIds = desired.Select(item => item.Id).ToHashSet();
        foreach (var orphan in entity.RegulatoryReferences.Where(item => !desiredIds.Contains(item.Id)).ToList())
        {
            entity.RegulatoryReferences.Remove(orphan);
        }

        foreach (var domain in desired)
        {
            var existing = entity.RegulatoryReferences.FirstOrDefault(item => item.Id == domain.Id);
            if (existing is null)
            {
                entity.RegulatoryReferences.Add(ToPersistenceRegulatoryReference(domain, entity.Id));
                continue;
            }

            existing.GazetteNumber = domain.GazetteNumber;
            existing.Title = domain.Title;
            existing.EffectiveDate = domain.EffectiveDate;
            existing.Summary = domain.Summary;
            existing.DataProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(domain.DataProvenance);
        }
    }

    private static Point CreatePoint(double longitude, double latitude) =>
        GeometryFactory.CreatePoint(new Coordinate(longitude, latitude));

    /// <summary>
    /// Corrects legacy rows where hectare magnitudes were persisted with SquareMeters
    /// because OpenAPI clients defaulted to the first enum value (1 = SquareMeters).
    /// Values stored as genuine square meters for parcels under 1,000 m² are unchanged.
    /// </summary>
    private static AreaUnit ResolvePersistedAreaUnit(decimal areaValue, AreaUnit areaUnit)
    {
        if (areaUnit == AreaUnit.SquareMeters && areaValue is > 0 and < 1000m)
        {
            return AreaUnit.Hectares;
        }

        return areaUnit;
    }
}

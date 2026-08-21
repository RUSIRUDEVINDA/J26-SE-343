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
            Boundary = null,
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId,
            SoilType = parcel.Characteristics?.SoilType,
            TerrainDescription = parcel.Characteristics?.TerrainDescription,
            ElevationMeters = parcel.Characteristics?.ElevationMeters,
            SpatialConstraints = parcel.SpatialConstraints
                .Select(constraint => ToPersistenceConstraint(constraint, parcel.Id))
                .ToList(),
            InfrastructureFeatures = parcel.InfrastructureFeatures
                .Select(feature => ToPersistenceFeature(feature, parcel.Id))
                .ToList(),
            EnvironmentalRestrictions = parcel.EnvironmentalRestrictions
                .Select(restriction => ToPersistenceEnvironmentalRestriction(restriction, parcel.Id))
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
                : new LandCharacteristics(entity.SoilType, entity.TerrainDescription, entity.ElevationMeters));

        PersistenceEntityIdHelper.SetEntityId(parcel, entity.Id);

        foreach (var constraint in entity.SpatialConstraints)
        {
            var domainConstraint = new SpatialConstraint(constraint.Type, constraint.Description, constraint.Severity);
            PersistenceEntityIdHelper.SetEntityId(domainConstraint, constraint.Id);
            parcel.AddSpatialConstraint(domainConstraint);
        }

        foreach (var feature in entity.InfrastructureFeatures)
        {
            var domainFeature = new InfrastructureFeature(
                feature.Type,
                feature.Name,
                feature.DistanceMeters,
                feature.Description);
            PersistenceEntityIdHelper.SetEntityId(domainFeature, feature.Id);
            parcel.AddInfrastructureFeature(domainFeature);
        }

        foreach (var restriction in entity.EnvironmentalRestrictions)
        {
            parcel.AddEnvironmentalRestriction(ToDomainEnvironmentalRestriction(restriction));
        }

        return parcel;
    }

    public static EnvironmentalRestriction ToDomainEnvironmentalRestriction(EnvironmentalRestrictionEntity entity) =>
        PersistenceEntityIdHelper.SetEntityId(
            new EnvironmentalRestriction(entity.Type, entity.Description, entity.Severity),
            entity.Id);

    public static SpatialConstraint ToDomain(SpatialConstraintEntity entity) =>
        PersistenceEntityIdHelper.SetEntityId(
            new SpatialConstraint(entity.Type, entity.Description, entity.Severity),
            entity.Id);

    public static void ApplyUpdates(LandParcelEntity entity, LandParcel parcel)
    {
        entity.CurrentLandUseId = parcel.CurrentUse is null
            ? null
            : LandIntelligenceSeedData.GetLandUseId(parcel.CurrentUse.Type);
        entity.SoilType = parcel.Characteristics?.SoilType;
        entity.TerrainDescription = parcel.Characteristics?.TerrainDescription;
        entity.ElevationMeters = parcel.Characteristics?.ElevationMeters;
    }

    private static SpatialReference ToSpatialReference(LandParcelEntity entity)
    {
        var coordinateSystem = $"EPSG:{entity.SpatialReferenceSystemId}";
        return new SpatialReference(
            entity.Centroid.Y,
            entity.Centroid.X,
            coordinateSystem,
            boundaryReference: null);
    }

    private static SpatialConstraintEntity ToPersistenceConstraint(SpatialConstraint constraint, Guid parcelId) =>
        new()
        {
            Id = constraint.Id,
            LandParcelId = parcelId,
            Type = constraint.Type,
            Description = constraint.Description,
            Severity = constraint.Severity,
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
            Severity = restriction.Severity
        };

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

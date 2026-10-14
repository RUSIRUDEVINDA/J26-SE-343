using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

/// <summary>
/// Controlled synthetic GIS layers for PostGIS integration verification.
/// All records are explicitly labeled [SYNTHETIC] and must not be treated as official cadastral data.
/// Geometries use NetTopologySuite types (Point / MultiPolygon, SRID 4326) persisted via EF Core PostGIS columns.
/// </summary>
public sealed class SyntheticGisDataset
{
    public const string CadastralPrefix = "SYNTHETIC-GIS-";

    public const string ParcelACadastralNumber = CadastralPrefix + "PARCEL-A";
    public const string ParcelBCadastralNumber = CadastralPrefix + "PARCEL-B";
    public const string ParcelCCadastralNumber = CadastralPrefix + "PARCEL-C";
    public const string ParcelDCadastralNumber = CadastralPrefix + "PARCEL-D";
    public const string ParcelECadastralNumber = CadastralPrefix + "PARCEL-E";
    public const string ParcelFCadastralNumber = CadastralPrefix + "PARCEL-F";
    public const string ParcelGCadastralNumber = CadastralPrefix + "PARCEL-G";
    public const string ParcelHCadastralNumber = CadastralPrefix + "PARCEL-H";

    public const string DistrictBoundaryDescription = "[SYNTHETIC] Administrative District Boundary — Colombo Test District";
    public const string ProtectedAreaDescription = "[SYNTHETIC] Protected / Restricted Area — Test Reserve";
    public const string WaterBodyDescription = "[SYNTHETIC] Water Feature — Test Lake Polygon";

    public const string RoadNearParcelEName = "[SYNTHETIC] Road — ~2 km from Parcel E";
    public const string RoadNearParcelFName = "[SYNTHETIC] Road — ~8 km from Parcel F";
    public const string WaterSupplyPointName = "[SYNTHETIC] Water Supply Point — Parcel C";
    public const string RailwayName = "[SYNTHETIC] Railway — Parcel A";

    /// <summary>Expected road distance for Parcel E (meters).</summary>
    public const double ParcelERoadDistanceMeters = 2_000;

    /// <summary>Expected road distance for Parcel F (meters).</summary>
    public const double ParcelFRoadDistanceMeters = 8_000;

    /// <summary>Approximate expected boundary area for standard test parcels (m²).</summary>
    public const double ExpectedParcelAreaSquareMeters = 250_000;

    public static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    public Guid DistrictBoundaryConstraintId { get; private set; }
    public Guid ProtectedAreaConstraintId { get; private set; }
    public Guid WaterBodyConstraintId { get; private set; }

    public Guid ParcelAId { get; private set; }
    public Guid ParcelBId { get; private set; }
    public Guid ParcelCId { get; private set; }
    public Guid ParcelDId { get; private set; }
    public Guid ParcelEId { get; private set; }
    public Guid ParcelFId { get; private set; }
    public Guid ParcelGId { get; private set; }
    public Guid ParcelHId { get; private set; }

    public Guid ParcelERoadFeatureId { get; private set; }
    public Guid ParcelFRoadFeatureId { get; private set; }
    public Guid WaterSupplyFeatureId { get; private set; }
    public Guid RailwayFeatureId { get; private set; }

    public double ParcelACentroidLatitude { get; private set; }
    public double ParcelACentroidLongitude { get; private set; }
    public double ParcelBCentroidLatitude { get; private set; }
    public double ParcelBCentroidLongitude { get; private set; }

    public static SyntheticGisDataset Create()
    {
        var dataset = new SyntheticGisDataset();
        dataset.InitializeIdentifiers();
        return dataset;
    }

    public async Task SeedAsync(LandIntelligenceDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await RemoveAsync(dbContext, cancellationToken);

        var anchorParcel = CreateParcelEntity(
            ParcelACadastralNumber,
            centroidLongitude: 79.8612,
            centroidLatitude: 6.9271,
            CreateSquareBoundary(79.8605, 6.9264, sideDegrees: 0.0045));

        var parcelB = CreateParcelEntity(
            ParcelBCadastralNumber,
            centroidLongitude: 80.5000,
            centroidLatitude: 7.5000,
            CreateSquareBoundary(80.4990, 7.4990, sideDegrees: 0.0020));

        var parcelC = CreateParcelEntity(
            ParcelCCadastralNumber,
            centroidLongitude: 79.8610,
            centroidLatitude: 6.9270,
            CreateSquareBoundary(79.8595, 6.9255, sideDegrees: 0.0040));

        var parcelD = CreateParcelEntity(
            ParcelDCadastralNumber,
            centroidLongitude: 79.8720,
            centroidLatitude: 6.9400,
            CreateSquareBoundary(79.8710, 6.9390, sideDegrees: 0.0020));

        var parcelE = CreateParcelEntity(
            ParcelECadastralNumber,
            centroidLongitude: 79.8612,
            centroidLatitude: 6.9271,
            CreateSquareBoundary(79.8605, 6.9264, sideDegrees: 0.0045));

        var parcelF = CreateParcelEntity(
            ParcelFCadastralNumber,
            centroidLongitude: 79.8200,
            centroidLatitude: 6.9271,
            CreateSquareBoundary(79.8193, 6.9264, sideDegrees: 0.0045));

        var parcelG = CreateParcelEntity(
            ParcelGCadastralNumber,
            centroidLongitude: 79.8620,
            centroidLatitude: 6.9280,
            CreateSquareBoundary(79.8613, 6.9273, sideDegrees: 0.0045));

        var parcelH = CreateParcelEntity(
            ParcelHCadastralNumber,
            centroidLongitude: 80.5000,
            centroidLatitude: 7.5000,
            CreateSquareBoundary(80.4990, 7.4990, sideDegrees: 0.0020));

        ParcelACentroidLatitude = 6.9271;
        ParcelACentroidLongitude = 79.8612;
        ParcelBCentroidLatitude = 7.5000;
        ParcelBCentroidLongitude = 80.5000;

        var districtBoundary = CreateConstraintEntity(
            DistrictBoundaryConstraintId,
            anchorParcel.Id,
            SpatialConstraintType.BufferZone,
            DistrictBoundaryDescription,
            CreateAxisAlignedPolygon(
                (79.8400, 6.9000),
                (79.8800, 6.9000),
                (79.8800, 6.9500),
                (79.8400, 6.9500)));

        var protectedArea = CreateConstraintEntity(
            ProtectedAreaConstraintId,
            parcelC.Id,
            SpatialConstraintType.BufferZone,
            ProtectedAreaDescription,
            CreateAxisAlignedPolygon(
                (79.8580, 6.9240),
                (79.8640, 6.9240),
                (79.8640, 6.9300),
                (79.8580, 6.9300)));

        var waterBody = CreateConstraintEntity(
            WaterBodyConstraintId,
            parcelC.Id,
            SpatialConstraintType.BufferZone,
            WaterBodyDescription,
            CreateAxisAlignedPolygon(
                (79.8590, 6.9250),
                (79.8615, 6.9250),
                (79.8615, 6.9275),
                (79.8590, 6.9275)));

        var parcelERoad = CreateInfrastructureEntity(
            ParcelERoadFeatureId,
            parcelE.Id,
            InfrastructureFeatureType.Road,
            RoadNearParcelEName,
            CreatePoint(79.8612, 6.9271 + DegreesLatitudeForMeters(ParcelERoadDistanceMeters)),
            (decimal)ParcelERoadDistanceMeters);

        var parcelFRoad = CreateInfrastructureEntity(
            ParcelFRoadFeatureId,
            parcelF.Id,
            InfrastructureFeatureType.Road,
            RoadNearParcelFName,
            CreatePoint(79.8200, 6.9271 + DegreesLatitudeForMeters(ParcelFRoadDistanceMeters)),
            (decimal)ParcelFRoadDistanceMeters);

        var waterSupply = CreateInfrastructureEntity(
            WaterSupplyFeatureId,
            parcelC.Id,
            InfrastructureFeatureType.WaterSupply,
            WaterSupplyPointName,
            CreatePoint(79.8605, 6.9265),
            distanceMeters: 350m);

        var railway = CreateInfrastructureEntity(
            RailwayFeatureId,
            anchorParcel.Id,
            InfrastructureFeatureType.Railway,
            RailwayName,
            CreatePoint(79.8630, 6.9280),
            distanceMeters: 400m);

        dbContext.LandParcels.AddRange(
            anchorParcel,
            parcelB,
            parcelC,
            parcelD,
            parcelE,
            parcelF,
            parcelG,
            parcelH);

        dbContext.SpatialConstraints.AddRange(districtBoundary, protectedArea, waterBody);
        dbContext.InfrastructureFeatures.AddRange(parcelERoad, parcelFRoad, waterSupply, railway);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(LandIntelligenceDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.InfrastructureFeatures
            .Where(feature => feature.Name.StartsWith("[SYNTHETIC]"))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.SpatialConstraints
            .Where(constraint => constraint.Description.StartsWith("[SYNTHETIC]"))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.LandParcels
            .Where(parcel => parcel.CadastralNumber.StartsWith(CadastralPrefix))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void InitializeIdentifiers()
    {
        DistrictBoundaryConstraintId = Guid.NewGuid();
        ProtectedAreaConstraintId = Guid.NewGuid();
        WaterBodyConstraintId = Guid.NewGuid();

        ParcelAId = Guid.NewGuid();
        ParcelBId = Guid.NewGuid();
        ParcelCId = Guid.NewGuid();
        ParcelDId = Guid.NewGuid();
        ParcelEId = Guid.NewGuid();
        ParcelFId = Guid.NewGuid();
        ParcelGId = Guid.NewGuid();
        ParcelHId = Guid.NewGuid();

        ParcelERoadFeatureId = Guid.NewGuid();
        ParcelFRoadFeatureId = Guid.NewGuid();
        WaterSupplyFeatureId = Guid.NewGuid();
        RailwayFeatureId = Guid.NewGuid();
    }

    private LandParcelEntity CreateParcelEntity(
        string cadastralNumber,
        double centroidLongitude,
        double centroidLatitude,
        MultiPolygon boundary)
    {
        var id = cadastralNumber switch
        {
            ParcelACadastralNumber => ParcelAId,
            ParcelBCadastralNumber => ParcelBId,
            ParcelCCadastralNumber => ParcelCId,
            ParcelDCadastralNumber => ParcelDId,
            ParcelECadastralNumber => ParcelEId,
            ParcelFCadastralNumber => ParcelFId,
            ParcelGCadastralNumber => ParcelGId,
            ParcelHCadastralNumber => ParcelHId,
            _ => Guid.NewGuid()
        };

        return new LandParcelEntity
        {
            Id = id,
            CadastralNumber = cadastralNumber,
            SurveyPlanReference = "[SYNTHETIC] GIS-TEST-PLAN",
            LandCategoryId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001"),
            CurrentLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000001"),
            AreaValue = 2.5m,
            AreaUnit = AreaUnit.Hectares,
            Province = "[SYNTHETIC] Western",
            District = "[SYNTHETIC] Colombo",
            DivisionalSecretariat = "[SYNTHETIC] Colombo DS",
            GramaNiladhariDivision = "[SYNTHETIC] GIS-Test-GN",
            Centroid = CreatePoint(centroidLongitude, centroidLatitude),
            Boundary = boundary,
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId,
            SoilType = "[SYNTHETIC] Test soil",
            TerrainDescription = "[SYNTHETIC] Flat test terrain",
            ElevationMeters = 10m
        };
    }

    private static SpatialConstraintEntity CreateConstraintEntity(
        Guid id,
        Guid landParcelId,
        SpatialConstraintType type,
        string description,
        MultiPolygon geometry) =>
        new()
        {
            Id = id,
            LandParcelId = landParcelId,
            Type = type,
            Description = description,
            Severity = RestrictionSeverity.Medium,
            ConstraintGeometry = geometry,
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId
        };

    private static InfrastructureFeatureEntity CreateInfrastructureEntity(
        Guid id,
        Guid landParcelId,
        InfrastructureFeatureType type,
        string name,
        Point location,
        decimal? distanceMeters) =>
        new()
        {
            Id = id,
            LandParcelId = landParcelId,
            Type = type,
            Name = name,
            Description = "[SYNTHETIC] Infrastructure feature for PostGIS integration tests.",
            Location = location,
            DistanceMeters = distanceMeters,
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId
        };

    private static Point CreatePoint(double longitude, double latitude) =>
        GeometryFactory.CreatePoint(new Coordinate(longitude, latitude));

    private static MultiPolygon CreateSquareBoundary(
        double minLongitude,
        double minLatitude,
        double sideDegrees) =>
        CreateAxisAlignedPolygon(
            (minLongitude, minLatitude),
            (minLongitude + sideDegrees, minLatitude),
            (minLongitude + sideDegrees, minLatitude + sideDegrees),
            (minLongitude, minLatitude + sideDegrees));

    private static MultiPolygon CreateAxisAlignedPolygon(params (double Longitude, double Latitude)[] ring)
    {
        var coordinates = ring
            .Select(point => new Coordinate(point.Longitude, point.Latitude))
            .ToList();

        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
        }

        var polygon = GeometryFactory.CreatePolygon(coordinates.ToArray());
        return GeometryFactory.CreateMultiPolygon(new[] { polygon });
    }

    /// <summary>Approximate latitude degrees for a north-south distance at ~7°N.</summary>
    private static double DegreesLatitudeForMeters(double meters) => meters / 111_320d;
}

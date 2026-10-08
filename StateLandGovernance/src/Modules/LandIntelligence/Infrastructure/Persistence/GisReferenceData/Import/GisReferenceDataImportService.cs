using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

public sealed class GisReferenceDataImportService : IGisReferenceDataImportService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILogger<GisReferenceDataImportService> _logger;

    public GisReferenceDataImportService(
        LandIntelligenceDbContext dbContext,
        ILogger<GisReferenceDataImportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<GisReferenceDataImportResult> ImportHambantotaPilotAsync(
        string? dataRootPath = null,
        CancellationToken cancellationToken = default)
    {
        var importContext = new GisReferenceImportContext(
            _dbContext,
            _logger,
            GisReferenceDataPaths.ResolveDataRoot(dataRootPath),
            DateTimeOffset.UtcNow);

        importContext.ImportDistrictAndProvinceBoundaries();
        importContext.ImportPilotFilteredLayers();

        await importContext.SaveChangesAsync(cancellationToken);

        return await importContext.BuildResultAsync(cancellationToken);
    }
}

internal sealed class GisReferenceImportContext
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly string _dataRoot;
    private readonly DateTimeOffset _importedAt;
    private readonly List<string> _skipLog = [];
    private int _imported;
    private int _updated;
    private int _skipped;

    private MultiPolygon? _hambantotaBoundary;
    private string _hambantotaDistrictName = GisReferenceDataPaths.HambantotaDistrictName;
    private string _provinceName = string.Empty;

    public GisReferenceImportContext(
        LandIntelligenceDbContext dbContext,
        ILogger logger,
        string dataRoot,
        DateTimeOffset importedAt)
    {
        _dbContext = dbContext;
        _logger = logger;
        _dataRoot = dataRoot;
        _importedAt = importedAt;
    }

    public void ImportDistrictAndProvinceBoundaries()
    {
        var districtCollection = ReadDataset("boundaries/district_boundaries.geojson");
        var hambantotaFeature = districtCollection
            .Cast<IFeature>()
            .FirstOrDefault(IsHambantotaDistrictFeature)
            ?? throw new InvalidDataException(
                $"Hambantota district feature was not found in '{GisReferenceDataPaths.DistrictBoundariesLayer}'.");

        _hambantotaDistrictName = GisReferenceAttributeReader.ReadString(hambantotaFeature, "district_name")
            ?? GisReferenceDataPaths.HambantotaDistrictName;

        var districtBoundary = RequireMultiPolygon(
            hambantotaFeature,
            GisReferenceDataPaths.DistrictBoundariesLayer,
            GisGeometryNormalizer.ToMultiPolygon(hambantotaFeature.Geometry),
            "Hambantota district boundary");

        _hambantotaBoundary = districtBoundary;

        UpsertAdministrativeBoundary(
            hambantotaFeature,
            GisReferenceDataPaths.DistrictBoundariesLayer,
            _hambantotaDistrictName,
            GisAdministrativeBoundaryType.District,
            districtBoundary);

        var expectedProvinceName = GisReferenceAttributeReader.ReadString(hambantotaFeature, "province_name");
        var provinceCollection = ReadDataset("boundaries/province_boundaries.geojson");
        var provinceFeature = provinceCollection
            .Cast<IFeature>()
            .FirstOrDefault(feature =>
                string.Equals(
                    GisReferenceAttributeReader.ReadString(feature, "province_name"),
                    expectedProvinceName,
                    StringComparison.OrdinalIgnoreCase))
            ?? provinceCollection
                .Cast<IFeature>()
                .FirstOrDefault(feature =>
                    GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, districtBoundary));

        if (provinceFeature is null)
        {
            throw new InvalidDataException(
                $"Province boundary for Hambantota was not found in '{GisReferenceDataPaths.ProvinceBoundariesLayer}'.");
        }

        _provinceName = GisReferenceAttributeReader.ReadString(provinceFeature, "province_name")
            ?? expectedProvinceName
            ?? "Unknown";

        var provinceBoundary = RequireMultiPolygon(
            provinceFeature,
            GisReferenceDataPaths.ProvinceBoundariesLayer,
            GisGeometryNormalizer.ToMultiPolygon(provinceFeature.Geometry),
            _provinceName);

        UpsertAdministrativeBoundary(
            provinceFeature,
            GisReferenceDataPaths.ProvinceBoundariesLayer,
            _provinceName,
            GisAdministrativeBoundaryType.Province,
            provinceBoundary);
    }

    public void ImportPilotFilteredLayers()
    {
        if (_hambantotaBoundary is null)
        {
            throw new InvalidOperationException("Hambantota district boundary must be imported first.");
        }

        ImportRoads(_hambantotaBoundary);
        ImportWaterFeatures("water/canals.geojson", GisReferenceDataPaths.CanalsLayer, GisWaterFeatureType.Canal);
        ImportWaterFeatures("water/lakes.geojson", GisReferenceDataPaths.LakesLayer, GisWaterFeatureType.Lake);
        ImportSoilGroups(_hambantotaBoundary);
        ImportSoilConservationAreas(_hambantotaBoundary);
        ImportSoilErosionObservations(_hambantotaBoundary);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<GisReferenceDataImportResult> BuildResultAsync(CancellationToken cancellationToken) =>
        new()
        {
            TableCounts = new Dictionary<string, int>
            {
                ["gis_administrative_boundaries"] = await _dbContext.GisAdministrativeBoundaries.CountAsync(cancellationToken),
                ["gis_roads"] = await _dbContext.GisRoads.CountAsync(cancellationToken),
                ["gis_water_features"] = await _dbContext.GisWaterFeatures.CountAsync(cancellationToken),
                ["gis_soil_groups"] = await _dbContext.GisSoilGroups.CountAsync(cancellationToken),
                ["gis_soil_conservation_areas"] = await _dbContext.GisSoilConservationAreas.CountAsync(cancellationToken),
                ["gis_soil_erosion_observations"] = await _dbContext.GisSoilErosionObservations.CountAsync(cancellationToken)
            },
            FeaturesImported = _imported,
            FeaturesUpdated = _updated,
            FeaturesSkipped = _skipped,
            SkipLog = _skipLog,
            HambantotaDistrictName = _hambantotaDistrictName,
            ProvinceName = _provinceName
        };

    private FeatureCollection ReadDataset(string relativePath) =>
        GeoJsonFeatureCollectionReader.Read(
            GisReferenceDataPaths.ResolveDatasetPath(_dataRoot, relativePath));

    private void ImportRoads(MultiPolygon pilotArea)
    {
        foreach (var feature in ReadDataset("transport/expressways.geojson").Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, pilotArea))
            {
                continue;
            }

            var geometry = TryMultiLineString(
                feature,
                GisReferenceDataPaths.ExpresswaysLayer,
                GisGeometryNormalizer.ToMultiLineString(feature.Geometry),
                GisReferenceAttributeReader.ReadString(feature, "road_name") ?? "expressway");

            if (geometry is null)
            {
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = ResolveIdentity(
                feature,
                GisReferenceDataPaths.ExpresswaysLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, "road_name")}");

            var entity = Upsert(
                FindExisting(_dbContext.GisRoads, GisReferenceDataPaths.ExpresswaysLayer, sourceFeatureId, sourceFingerprint),
                () => new GisRoadEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = GisReferenceDataPaths.ExpresswaysLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                },
                _dbContext.GisRoads);

            entity.Name = GisReferenceAttributeReader.ReadString(feature, "road_name");
            entity.RoadType = GisRoadType.Expressway;
            entity.Geometry = geometry;
            entity.ImportedAt = _importedAt;
        }
    }

    private void ImportWaterFeatures(string relativePath, string sourceLayer, GisWaterFeatureType featureType)
    {
        var nameAttribute = featureType == GisWaterFeatureType.Canal ? "canal_name" : "lake_name";

        foreach (var feature in ReadDataset(relativePath).Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, _hambantotaBoundary!))
            {
                continue;
            }

            var geometry = TryGeometry(
                feature,
                sourceLayer,
                GisGeometryNormalizer.ToWaterGeometry(feature.Geometry),
                GisReferenceAttributeReader.ReadString(feature, nameAttribute) ?? featureType.ToString());

            if (geometry is null)
            {
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = ResolveIdentity(
                feature,
                sourceLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, nameAttribute)}");

            var entity = Upsert(
                FindExisting(_dbContext.GisWaterFeatures, sourceLayer, sourceFeatureId, sourceFingerprint),
                () => new GisWaterFeatureEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = sourceLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                },
                _dbContext.GisWaterFeatures);

            entity.Name = GisReferenceAttributeReader.ReadString(feature, nameAttribute);
            entity.FeatureType = featureType;
            entity.Geometry = geometry;
            entity.ImportedAt = _importedAt;
        }
    }

    private void ImportSoilGroups(MultiPolygon pilotArea)
    {
        foreach (var feature in ReadDataset("soil/soil_groups.geojson").Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, pilotArea))
            {
                continue;
            }

            var geometry = TryMultiPolygon(
                feature,
                GisReferenceDataPaths.SoilGroupsLayer,
                GisGeometryNormalizer.ToMultiPolygon(feature.Geometry),
                GisReferenceAttributeReader.ReadString(feature, "name") ?? "soil group");

            if (geometry is null)
            {
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = ResolveIdentity(
                feature,
                GisReferenceDataPaths.SoilGroupsLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, "name")}");

            var entity = Upsert(
                FindExisting(_dbContext.GisSoilGroups, GisReferenceDataPaths.SoilGroupsLayer, sourceFeatureId, sourceFingerprint),
                () => new GisSoilGroupEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = GisReferenceDataPaths.SoilGroupsLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                },
                _dbContext.GisSoilGroups);

            entity.Name = GisReferenceAttributeReader.ReadString(feature, "name") ?? "Unknown";
            entity.Boundary = geometry;
            entity.ImportedAt = _importedAt;
        }
    }

    private void ImportSoilConservationAreas(MultiPolygon pilotArea)
    {
        foreach (var feature in ReadDataset("soil/soil_conservation_areas.geojson").Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, pilotArea))
            {
                continue;
            }

            var geometry = TryMultiPolygon(
                feature,
                GisReferenceDataPaths.SoilConservationAreasLayer,
                GisGeometryNormalizer.ToMultiPolygon(feature.Geometry),
                GisReferenceAttributeReader.ReadString(feature, "description") ?? "soil conservation area");

            if (geometry is null)
            {
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = ResolveIdentity(
                feature,
                GisReferenceDataPaths.SoilConservationAreasLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, "id")}|{GisReferenceAttributeReader.ReadString(feature, "description")}");

            var entity = Upsert(
                FindExisting(_dbContext.GisSoilConservationAreas, GisReferenceDataPaths.SoilConservationAreasLayer, sourceFeatureId, sourceFingerprint),
                () => new GisSoilConservationAreaEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = GisReferenceDataPaths.SoilConservationAreasLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                },
                _dbContext.GisSoilConservationAreas);

            entity.Name = GisReferenceAttributeReader.ReadString(feature, "id") is { Length: > 0 } id
                ? $"Conservation Area {id}"
                : $"Conservation Area {sourceFeatureId ?? sourceFingerprint?[..8]}";
            entity.Description = GisReferenceAttributeReader.ReadString(feature, "description");
            entity.Boundary = geometry;
            entity.ImportedAt = _importedAt;
        }
    }

    private void ImportSoilErosionObservations(MultiPolygon pilotArea)
    {
        foreach (var feature in ReadDataset("soil/soil_erosion.geojson").Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, pilotArea))
            {
                continue;
            }

            var geometry = TryPoint(
                feature,
                GisReferenceDataPaths.SoilErosionLayer,
                GisGeometryNormalizer.ToPoint(feature.Geometry),
                GisReferenceAttributeReader.ReadString(feature, "erosion_site_id") ?? "erosion observation");

            if (geometry is null)
            {
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = ResolveIdentity(
                feature,
                GisReferenceDataPaths.SoilErosionLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, "erosion_site_id")}|{GisReferenceAttributeReader.ReadString(feature, "erosion_rate")}");

            var entity = Upsert(
                FindExisting(_dbContext.GisSoilErosionObservations, GisReferenceDataPaths.SoilErosionLayer, sourceFeatureId, sourceFingerprint),
                () => new GisSoilErosionObservationEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = GisReferenceDataPaths.SoilErosionLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                },
                _dbContext.GisSoilErosionObservations);

            entity.ObservationClass = GisReferenceAttributeReader.ReadString(feature, "erosion_site_id");
            entity.Description = GisReferenceAttributeReader.ReadString(feature, "description");
            entity.ErosionRate = GisReferenceAttributeReader.ReadDecimal(feature, "erosion_rate");
            entity.Location = geometry;
            entity.ImportedAt = _importedAt;
        }
    }

    private void UpsertAdministrativeBoundary(
        IFeature feature,
        string sourceLayer,
        string name,
        GisAdministrativeBoundaryType boundaryType,
        MultiPolygon boundary)
    {
        var (sourceFeatureId, sourceFingerprint) = ResolveIdentity(
            feature,
            sourceLayer,
            $"{boundary.AsText()}|{name}|{boundaryType}");

        var entity = Upsert(
            FindExisting(_dbContext.GisAdministrativeBoundaries, sourceLayer, sourceFeatureId, sourceFingerprint),
            () => new GisAdministrativeBoundaryEntity
            {
                Id = Guid.NewGuid(),
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = sourceLayer,
                SourceFeatureId = sourceFeatureId,
                SourceFingerprint = sourceFingerprint
            },
            _dbContext.GisAdministrativeBoundaries);

        entity.Name = name;
        entity.BoundaryType = boundaryType;
        entity.Boundary = boundary;
        entity.ImportedAt = _importedAt;
    }

    private TEntity Upsert<TEntity>(TEntity? existing, Func<TEntity> create, DbSet<TEntity> dbSet)
        where TEntity : class
    {
        if (existing is not null)
        {
            _updated++;
            return existing;
        }

        var entity = create();
        dbSet.Add(entity);
        _imported++;
        return entity;
    }

    private MultiPolygon? TryMultiPolygon(
        IFeature feature,
        string sourceLayer,
        MultiPolygon? geometry,
        string label)
    {
        if (geometry is not null)
        {
            return geometry;
        }

        Skip(sourceLayer, feature, $"{label}: invalid or unsupported geometry ({GisGeometryNormalizer.DescribeGeometry(feature.Geometry)})");
        return null;
    }

    private MultiLineString? TryMultiLineString(
        IFeature feature,
        string sourceLayer,
        MultiLineString? geometry,
        string label)
    {
        if (geometry is not null)
        {
            return geometry;
        }

        Skip(sourceLayer, feature, $"{label}: invalid or unsupported geometry ({GisGeometryNormalizer.DescribeGeometry(feature.Geometry)})");
        return null;
    }

    private Point? TryPoint(
        IFeature feature,
        string sourceLayer,
        Point? geometry,
        string label)
    {
        if (geometry is not null)
        {
            return geometry;
        }

        Skip(sourceLayer, feature, $"{label}: invalid or unsupported geometry ({GisGeometryNormalizer.DescribeGeometry(feature.Geometry)})");
        return null;
    }

    private Geometry? TryGeometry(
        IFeature feature,
        string sourceLayer,
        Geometry? geometry,
        string label)
    {
        if (geometry is not null)
        {
            return geometry;
        }

        Skip(sourceLayer, feature, $"{label}: invalid or unsupported geometry ({GisGeometryNormalizer.DescribeGeometry(feature.Geometry)})");
        return null;
    }

    private MultiPolygon RequireMultiPolygon(
        IFeature feature,
        string sourceLayer,
        MultiPolygon? geometry,
        string label)
    {
        if (geometry is not null)
        {
            return geometry;
        }

        Skip(sourceLayer, feature, $"{label}: invalid or unsupported geometry ({GisGeometryNormalizer.DescribeGeometry(feature.Geometry)})");
        throw new InvalidDataException($"{label} geometry is invalid or unsupported.");
    }

    private (string? SourceFeatureId, string? SourceFingerprint) ResolveIdentity(
        IFeature feature,
        string sourceLayer,
        string fingerprintSeed) =>
        GisReferenceImportIdentityResolver.Resolve(
            feature,
            GisReferenceDataPaths.SourceName,
            sourceLayer,
            fingerprintSeed);

    private static bool IsHambantotaDistrictFeature(IFeature feature) =>
        string.Equals(
            GisReferenceAttributeReader.ReadString(feature, "district_name"),
            GisReferenceDataPaths.HambantotaDistrictName,
            StringComparison.OrdinalIgnoreCase);

    private void Skip(string sourceLayer, IFeature feature, string reason)
    {
        var identifier = GisReferenceImportIdentityResolver.ResolveSourceFeatureId(feature) ?? "unknown";
        var message = $"{sourceLayer}/{identifier}: {reason}";
        _skipLog.Add(message);
        _skipped++;
        _logger.LogWarning("Skipped GIS reference feature {Message}", message);
    }

    private TEntity? FindExisting<TEntity>(
        DbSet<TEntity> dbSet,
        string sourceLayer,
        string? sourceFeatureId,
        string? sourceFingerprint)
        where TEntity : GisReferenceEntityBase
    {
        if (!string.IsNullOrWhiteSpace(sourceFeatureId))
        {
            return dbSet.Local.FirstOrDefault(entity =>
                    entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == sourceFeatureId)
                ?? dbSet.FirstOrDefault(entity =>
                    entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == sourceFeatureId);
        }

        if (!string.IsNullOrWhiteSpace(sourceFingerprint))
        {
            return dbSet.Local.FirstOrDefault(entity =>
                    entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == null
                    && entity.SourceFingerprint == sourceFingerprint)
                ?? dbSet.FirstOrDefault(entity =>
                    entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == null
                    && entity.SourceFingerprint == sourceFingerprint);
        }

        return null;
    }
}

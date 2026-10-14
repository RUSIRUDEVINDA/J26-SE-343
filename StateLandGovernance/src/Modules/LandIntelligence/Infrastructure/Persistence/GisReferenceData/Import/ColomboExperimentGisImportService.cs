using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

/// <summary>
/// Imports Colombo-relevant GIS experiment layers (water/soil/conservation) clipped by
/// spatial relevance to the Colombo district boundary. Does not claim nationwide coverage.
/// </summary>
public sealed class ColomboExperimentGisImportService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILogger<ColomboExperimentGisImportService> _logger;

    public ColomboExperimentGisImportService(
        LandIntelligenceDbContext dbContext,
        ILogger<ColomboExperimentGisImportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ColomboExperimentLayerImportResult> ImportWaterSoilConservationForDistrictAsync(
        string districtName,
        string? dataRootPath = null,
        CancellationToken cancellationToken = default)
    {
        var dataRoot = GisReferenceDataPaths.ResolveDataRoot(dataRootPath);
        var boundary = await _dbContext.GisAdministrativeBoundaries
            .AsNoTracking()
            .Where(entity =>
                entity.BoundaryType == GisAdministrativeBoundaryType.District
                && entity.Name == districtName)
            .Select(entity => entity.Boundary)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"District boundary '{districtName}' must be imported before experiment layers.");

        var importedAt = DateTimeOffset.UtcNow;
        var summary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var skipLog = new List<string>();

        summary["canals"] = await ImportWaterAsync(
            dataRoot,
            "water/canals.geojson",
            GisReferenceDataPaths.CanalsLayer,
            GisWaterFeatureType.Canal,
            "canal_name",
            boundary,
            importedAt,
            skipLog,
            cancellationToken);

        summary["lakes"] = await ImportWaterAsync(
            dataRoot,
            "water/lakes.geojson",
            GisReferenceDataPaths.LakesLayer,
            GisWaterFeatureType.Lake,
            "lake_name",
            boundary,
            importedAt,
            skipLog,
            cancellationToken);

        summary["soil_groups"] = await ImportSoilGroupsAsync(
            dataRoot,
            boundary,
            importedAt,
            skipLog,
            cancellationToken);

        summary["soil_conservation_areas"] = await ImportConservationAsync(
            dataRoot,
            boundary,
            importedAt,
            skipLog,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Colombo experiment layer import for {District}: {Summary}",
            districtName,
            string.Join(", ", summary.Select(kv => $"{kv.Key}={kv.Value}")));

        return new ColomboExperimentLayerImportResult
        {
            DistrictName = districtName,
            LayerUpsertCounts = summary,
            SkipLog = skipLog,
            CoverageNote =
                "Layers were imported only where geometries intersect the district boundary. " +
                "This does not prove complete geographic coverage of Colombo for every theme; " +
                "record emptiness / sparse layers as missing source data, not assessed success."
        };
    }

    private async Task<int> ImportWaterAsync(
        string dataRoot,
        string relativePath,
        string sourceLayer,
        GisWaterFeatureType featureType,
        string nameAttribute,
        MultiPolygon boundary,
        DateTimeOffset importedAt,
        List<string> skipLog,
        CancellationToken cancellationToken)
    {
        var path = GisReferenceDataPaths.ResolveDatasetPath(dataRoot, relativePath);
        if (!File.Exists(path))
        {
            skipLog.Add($"{sourceLayer}: file missing at {path}");
            return 0;
        }

        var upserts = 0;
        foreach (var feature in GeoJsonFeatureCollectionReader.Read(path).Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, boundary))
            {
                continue;
            }

            var geometry = GisGeometryNormalizer.ToWaterGeometry(feature.Geometry);
            if (geometry is null)
            {
                skipLog.Add($"{sourceLayer}: invalid geometry");
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = GisReferenceImportIdentityResolver.Resolve(
                feature,
                GisReferenceDataPaths.SourceName,
                sourceLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, nameAttribute)}");

            var existing = await FindExistingAsync(
                _dbContext.GisWaterFeatures,
                sourceLayer,
                sourceFeatureId,
                sourceFingerprint,
                cancellationToken);

            if (existing is null)
            {
                existing = new GisWaterFeatureEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = sourceLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                };
                _dbContext.GisWaterFeatures.Add(existing);
            }

            existing.Name = GisReferenceAttributeReader.ReadString(feature, nameAttribute);
            existing.FeatureType = featureType;
            existing.Geometry = geometry;
            existing.ImportedAt = importedAt;
            upserts++;
        }

        return upserts;
    }

    private async Task<int> ImportSoilGroupsAsync(
        string dataRoot,
        MultiPolygon boundary,
        DateTimeOffset importedAt,
        List<string> skipLog,
        CancellationToken cancellationToken)
    {
        var path = GisReferenceDataPaths.ResolveDatasetPath(dataRoot, "soil/soil_groups.geojson");
        if (!File.Exists(path))
        {
            skipLog.Add($"{GisReferenceDataPaths.SoilGroupsLayer}: file missing at {path}");
            return 0;
        }

        var upserts = 0;
        foreach (var feature in GeoJsonFeatureCollectionReader.Read(path).Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, boundary))
            {
                continue;
            }

            var geometry = GisGeometryNormalizer.ToMultiPolygon(feature.Geometry);
            if (geometry is null)
            {
                skipLog.Add($"{GisReferenceDataPaths.SoilGroupsLayer}: invalid geometry");
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = GisReferenceImportIdentityResolver.Resolve(
                feature,
                GisReferenceDataPaths.SourceName,
                GisReferenceDataPaths.SoilGroupsLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, "name")}");

            var existing = await FindExistingAsync(
                _dbContext.GisSoilGroups,
                GisReferenceDataPaths.SoilGroupsLayer,
                sourceFeatureId,
                sourceFingerprint,
                cancellationToken);

            if (existing is null)
            {
                existing = new GisSoilGroupEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = GisReferenceDataPaths.SoilGroupsLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                };
                _dbContext.GisSoilGroups.Add(existing);
            }

            existing.Name = GisReferenceAttributeReader.ReadString(feature, "name") ?? "Unknown";
            existing.Boundary = geometry;
            existing.ImportedAt = importedAt;
            upserts++;
        }

        return upserts;
    }

    private async Task<int> ImportConservationAsync(
        string dataRoot,
        MultiPolygon boundary,
        DateTimeOffset importedAt,
        List<string> skipLog,
        CancellationToken cancellationToken)
    {
        var path = GisReferenceDataPaths.ResolveDatasetPath(
            dataRoot,
            "soil/soil_conservation_areas.geojson");
        if (!File.Exists(path))
        {
            skipLog.Add($"{GisReferenceDataPaths.SoilConservationAreasLayer}: file missing at {path}");
            return 0;
        }

        var upserts = 0;
        foreach (var feature in GeoJsonFeatureCollectionReader.Read(path).Cast<IFeature>())
        {
            if (!GisGeometryNormalizer.IsSpatiallyRelevant(feature.Geometry, boundary))
            {
                continue;
            }

            var geometry = GisGeometryNormalizer.ToMultiPolygon(feature.Geometry);
            if (geometry is null)
            {
                skipLog.Add($"{GisReferenceDataPaths.SoilConservationAreasLayer}: invalid geometry");
                continue;
            }

            var (sourceFeatureId, sourceFingerprint) = GisReferenceImportIdentityResolver.Resolve(
                feature,
                GisReferenceDataPaths.SourceName,
                GisReferenceDataPaths.SoilConservationAreasLayer,
                $"{geometry.AsText()}|{GisReferenceAttributeReader.ReadString(feature, "id")}|{GisReferenceAttributeReader.ReadString(feature, "description")}");

            var existing = await FindExistingAsync(
                _dbContext.GisSoilConservationAreas,
                GisReferenceDataPaths.SoilConservationAreasLayer,
                sourceFeatureId,
                sourceFingerprint,
                cancellationToken);

            if (existing is null)
            {
                existing = new GisSoilConservationAreaEntity
                {
                    Id = Guid.NewGuid(),
                    SourceName = GisReferenceDataPaths.SourceName,
                    SourceLayer = GisReferenceDataPaths.SoilConservationAreasLayer,
                    SourceFeatureId = sourceFeatureId,
                    SourceFingerprint = sourceFingerprint
                };
                _dbContext.GisSoilConservationAreas.Add(existing);
            }

            existing.Name = GisReferenceAttributeReader.ReadString(feature, "id") is { Length: > 0 } id
                ? id
                : "conservation-area";
            existing.Description = GisReferenceAttributeReader.ReadString(feature, "description");
            existing.Boundary = geometry;
            existing.ImportedAt = importedAt;
            upserts++;
        }

        return upserts;
    }

    private async Task<TEntity?> FindExistingAsync<TEntity>(
        DbSet<TEntity> dbSet,
        string sourceLayer,
        string? sourceFeatureId,
        string? sourceFingerprint,
        CancellationToken cancellationToken)
        where TEntity : GisReferenceEntityBase
    {
        if (!string.IsNullOrWhiteSpace(sourceFeatureId))
        {
            return await dbSet.FirstOrDefaultAsync(
                entity => entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == sourceFeatureId,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(sourceFingerprint))
        {
            return await dbSet.FirstOrDefaultAsync(
                entity => entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == null
                    && entity.SourceFingerprint == sourceFingerprint,
                cancellationToken);
        }

        return null;
    }
}

public sealed class ColomboExperimentLayerImportResult
{
    public required string DistrictName { get; init; }

    public required IReadOnlyDictionary<string, int> LayerUpsertCounts { get; init; }

    public required IReadOnlyList<string> SkipLog { get; init; }

    public required string CoverageNote { get; init; }
}

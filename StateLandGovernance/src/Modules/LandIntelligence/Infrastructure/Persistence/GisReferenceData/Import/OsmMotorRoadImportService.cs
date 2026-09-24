using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

/// <summary>
/// Local-development import of prepared OSM motor roads (SourceLayer = osm_motor_roads).
/// Does not modify expressways rows. Nationwide geometries are retained so nearest-road
/// queries near Colombo can select roads outside the district boundary.
/// </summary>
public sealed class OsmMotorRoadImportService : IOsmMotorRoadImportService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILogger<OsmMotorRoadImportService> _logger;
    private readonly GisEnrichmentCoverageOptions _coverageOptions;

    public OsmMotorRoadImportService(
        LandIntelligenceDbContext dbContext,
        ILogger<OsmMotorRoadImportService> logger,
        IOptions<GisEnrichmentCoverageOptions> coverageOptions)
    {
        _dbContext = dbContext;
        _logger = logger;
        _coverageOptions = coverageOptions.Value;
    }

    public async Task<OsmMotorRoadImportResult> ImportFromGeoJsonAsync(
        string geoJsonPath,
        string? filterPolicyVersion = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(geoJsonPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"OSM motor-road GeoJSON was not found at '{fullPath}'.", fullPath);
        }

        var policyVersion = string.IsNullOrWhiteSpace(filterPolicyVersion)
            ? _coverageOptions.OsmMotorRoadFilterPolicyVersion
                ?? GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion
            : filterPolicyVersion.Trim();

        var importedAt = DateTimeOffset.UtcNow;
        var skipLog = new List<string>();
        var imported = 0;
        var updated = 0;
        var skipped = 0;

        var collection = GeoJsonFeatureCollectionReader.Read(fullPath);
        const string sourceLayer = GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer;
        const string sourceName = GisEnrichmentCoverageDefaults.OsmMotorRoadSourceName;
        const int batchSize = 500;

        var existingIds = (await _dbContext.GisRoads
                .AsNoTracking()
                .Where(entity => entity.SourceName == sourceName && entity.SourceLayer == sourceLayer)
                .Select(entity => entity.SourceFeatureId!)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var pendingInBatch = 0;
        foreach (var feature in collection.Cast<IFeature>())
        {
            var osmId = GisReferenceAttributeReader.ReadString(feature, "osm_id")
                ?? GisReferenceAttributeReader.ReadString(feature, "id");
            var highway = GisReferenceAttributeReader.ReadString(feature, "highway");

            if (string.IsNullOrWhiteSpace(osmId) || string.IsNullOrWhiteSpace(highway))
            {
                skipped++;
                if (skipLog.Count < 50)
                {
                    skipLog.Add($"{sourceLayer}/unknown: missing osm_id or highway");
                }

                continue;
            }

            if (existingIds.Contains(osmId))
            {
                // Idempotent path: identity already present — do not rewrite geometry on every pass.
                updated++;
                continue;
            }

            var geometry = GisGeometryNormalizer.ToMultiLineString(feature.Geometry);
            if (geometry is null)
            {
                skipped++;
                if (skipLog.Count < 50)
                {
                    skipLog.Add($"{sourceLayer}/{osmId}: invalid or unsupported geometry");
                }

                continue;
            }

            var displayName = OsmMotorRoadAttributeEncoding.ReadDisplayNameFromAttributes(
                key => GisReferenceAttributeReader.ReadString(feature, key));
            var encodedName = OsmMotorRoadAttributeEncoding.EncodeName(highway, displayName);
            var roadType = OsmMotorRoadAttributeEncoding.MapHighwayToRoadType(highway);

            _dbContext.GisRoads.Add(new GisRoadEntity
            {
                Id = Guid.NewGuid(),
                SourceName = sourceName,
                SourceLayer = sourceLayer,
                SourceFeatureId = osmId,
                SourceFingerprint = null,
                Name = encodedName,
                RoadType = roadType,
                Geometry = geometry,
                ImportedAt = importedAt
            });
            existingIds.Add(osmId);
            imported++;
            pendingInBatch++;

            if (pendingInBatch >= batchSize)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                pendingInBatch = 0;
            }
        }

        if (pendingInBatch > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }

        var total = await _dbContext.GisRoads.CountAsync(
            entity => entity.SourceName == sourceName && entity.SourceLayer == sourceLayer,
            cancellationToken);

        _logger.LogInformation(
            "OSM motor-road import complete: imported={Imported}, updated={Updated}, skipped={Skipped}, total={Total}, policy={Policy}",
            imported,
            updated,
            skipped,
            total,
            policyVersion);

        return new OsmMotorRoadImportResult
        {
            FeaturesImported = imported,
            FeaturesUpdated = updated,
            FeaturesSkipped = skipped,
            TotalOsmMotorRoadsInTable = total,
            SourceLayer = sourceLayer,
            FilterPolicyVersion = policyVersion,
            SourcePath = fullPath,
            SkipLog = skipLog,
            CoverageNote =
                "Nationwide (or beyond-Colombo) motor-road geometries are stored under SourceLayer=osm_motor_roads. " +
                "Nearest-road enrichment filters by this layer and must not mix expressways. " +
                "Colombo district coverage requires importing the Colombo administrative boundary separately; " +
                "road geometries themselves are intentionally not clipped to Colombo."
        };
    }

    public async Task<int> ImportDistrictBoundaryAsync(
        string districtName,
        string? dataRootPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(districtName);

        var dataRoot = GisReferenceDataPaths.ResolveDataRoot(dataRootPath);
        var path = GisReferenceDataPaths.ResolveDatasetPath(dataRoot, "boundaries/district_boundaries.geojson");
        var collection = GeoJsonFeatureCollectionReader.Read(path);
        var feature = collection
            .Cast<IFeature>()
            .FirstOrDefault(item =>
                string.Equals(
                    GisReferenceAttributeReader.ReadString(item, "district_name"),
                    districtName,
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                $"District '{districtName}' was not found in '{GisReferenceDataPaths.DistrictBoundariesLayer}' at '{path}'.");

        var name = GisReferenceAttributeReader.ReadString(feature, "district_name") ?? districtName.Trim();
        var boundary = GisGeometryNormalizer.ToMultiPolygon(feature.Geometry)
            ?? throw new InvalidDataException($"District '{name}' has invalid boundary geometry.");

        var (sourceFeatureId, sourceFingerprint) = GisReferenceImportIdentityResolver.Resolve(
            feature,
            GisReferenceDataPaths.SourceName,
            GisReferenceDataPaths.DistrictBoundariesLayer,
            $"{boundary.AsText()}|{name}|{GisAdministrativeBoundaryType.District}");

        var existing = await FindBoundaryAsync(
            GisReferenceDataPaths.DistrictBoundariesLayer,
            sourceFeatureId,
            sourceFingerprint,
            cancellationToken);

        if (existing is null)
        {
            existing = new GisAdministrativeBoundaryEntity
            {
                Id = Guid.NewGuid(),
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = GisReferenceDataPaths.DistrictBoundariesLayer,
                SourceFeatureId = sourceFeatureId,
                SourceFingerprint = sourceFingerprint
            };
            _dbContext.GisAdministrativeBoundaries.Add(existing);
        }

        existing.Name = name;
        existing.BoundaryType = GisAdministrativeBoundaryType.District;
        existing.Boundary = boundary;
        existing.ImportedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    public async Task<int> DeleteOsmMotorRoadsAsync(CancellationToken cancellationToken = default)
    {
        var roads = await _dbContext.GisRoads
            .Where(entity =>
                entity.SourceName == GisEnrichmentCoverageDefaults.OsmMotorRoadSourceName
                && entity.SourceLayer == GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer)
            .ToListAsync(cancellationToken);

        _dbContext.GisRoads.RemoveRange(roads);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return roads.Count;
    }

    private async Task<GisAdministrativeBoundaryEntity?> FindBoundaryAsync(
        string sourceLayer,
        string? sourceFeatureId,
        string? sourceFingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sourceFeatureId))
        {
            return await _dbContext.GisAdministrativeBoundaries.FirstOrDefaultAsync(
                entity => entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == sourceFeatureId,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(sourceFingerprint))
        {
            return await _dbContext.GisAdministrativeBoundaries.FirstOrDefaultAsync(
                entity => entity.SourceName == GisReferenceDataPaths.SourceName
                    && entity.SourceLayer == sourceLayer
                    && entity.SourceFeatureId == null
                    && entity.SourceFingerprint == sourceFingerprint,
                cancellationToken);
        }

        return null;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisCoverage;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.GisCoverage;

internal sealed class LandIntelligenceGisCoverageService : ILandIntelligenceGisCoverageService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly GisEnrichmentCoverageOptions _coverageOptions;
    private readonly TimeProvider _timeProvider;

    public LandIntelligenceGisCoverageService(
        LandIntelligenceDbContext dbContext,
        IOptions<GisEnrichmentCoverageOptions> coverageOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _coverageOptions = coverageOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<LandIntelligenceGisCoverageDto> MeasureCoverageAsync(
        CancellationToken cancellationToken = default)
    {
        var measuredAt = _timeProvider.GetUtcNow();
        var totalParcelCount = await _dbContext.LandParcels
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var indexedDistrictRows = await _dbContext.LandParcels
            .AsNoTracking()
            .GroupBy(parcel => new { parcel.Province, parcel.District })
            .Select(group => new
            {
                group.Key.Province,
                group.Key.District,
                ParcelCount = group.Count(),
            })
            .OrderByDescending(item => item.ParcelCount)
            .ThenBy(item => item.District)
            .ToListAsync(cancellationToken);

        var indexedDistricts = indexedDistrictRows
            .Select(item => new IndexedDistrictSummaryDto(
                item.Province,
                item.District,
                item.ParcelCount))
            .ToList();

        var configuredDistricts = GisEnrichmentCoverageHelper
            .ResolveSupportedDistricts(_coverageOptions)
            .ToList();

        var snapshotCounts = await _dbContext.LandParcelGisEnrichmentSnapshots
            .AsNoTracking()
            .GroupBy(snapshot => snapshot.OverallStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var enrichmentSnapshotCount = snapshotCounts.Sum(item => item.Count);
        var completeCount = snapshotCounts
            .Where(item => item.Status == LandParcelGisEnrichmentOverallStatus.Complete)
            .Sum(item => item.Count);
        var partialCount = snapshotCounts
            .Where(item => item.Status == LandParcelGisEnrichmentOverallStatus.Partial)
            .Sum(item => item.Count);
        var unavailableCount = snapshotCounts
            .Where(item => item.Status == LandParcelGisEnrichmentOverallStatus.Unavailable)
            .Sum(item => item.Count);

        var overallStatus = GisIntelligenceLayerCoverageClassifier.ClassifyOverallEnrichment(
            totalParcelCount,
            enrichmentSnapshotCount,
            completeCount,
            partialCount);

        var overallNotice = overallStatus switch
        {
            GisIntelligenceCoverageStatus.Unavailable =>
                "No unified GIS enrichment snapshots are stored for indexed parcels in this database, or enrichment did not complete. Missing GIS is not treated as a positive match in recommendations.",
            GisIntelligenceCoverageStatus.Partial =>
                $"GIS enrichment snapshots exist for {enrichmentSnapshotCount} of {totalParcelCount} parcels (complete: {completeCount}, partial: {partialCount}). Layers without evidence remain unknown.",
            GisIntelligenceCoverageStatus.Available =>
                "All indexed parcels have a complete GIS enrichment snapshot in this database. This reflects imported reference layers and enrichment runs only—not official government verification.",
            _ => "Enrichment status unknown.",
        };

        var districtBoundaryCount = await _dbContext.GisAdministrativeBoundaries
            .AsNoTracking()
            .CountAsync(
                boundary => boundary.BoundaryType == GisAdministrativeBoundaryType.District,
                cancellationToken);

        var adminParcelEvidence = await _dbContext.LandParcelGisEnrichmentSnapshots
            .AsNoTracking()
            .CountAsync(
                snapshot => snapshot.DetectedDistrict != null || snapshot.AdministrativeStatus != null,
                cancellationToken);

        var roadReferenceCount = await _dbContext.GisRoads.AsNoTracking().CountAsync(cancellationToken);
        var roadParcelEvidence = await CountDistinctParcelsWithGisDerivedInfrastructureAsync(
            InfrastructureFeatureType.Road,
            cancellationToken);

        var waterReferenceCount = await _dbContext.GisWaterFeatures.AsNoTracking().CountAsync(cancellationToken);
        var waterParcelEvidence = await CountDistinctParcelsWithGisDerivedInfrastructureAsync(
            InfrastructureFeatureType.Other,
            cancellationToken);

        var soilReferenceCount = await _dbContext.GisSoilGroups.AsNoTracking().CountAsync(cancellationToken);
        var soilParcelEvidence = await _dbContext.ParcelDerivedSoilGroups
            .AsNoTracking()
            .Select(item => item.LandParcelId)
            .Distinct()
            .CountAsync(cancellationToken);

        var conservationReferenceCount = await _dbContext.GisSoilConservationAreas
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var conservationParcelEvidence = await CountDistinctParcelsWithGisDerivedEnvironmentalAsync(
            EnvironmentalRestrictionType.ProtectedArea,
            cancellationToken);

        var erosionReferenceCount = await _dbContext.GisSoilErosionObservations
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var erosionParcelEvidence = 0;

        var configuredRoadLayer = _coverageOptions.ResolveRoadSourceLayerForDistrict(null);
        var roadLayerLabel = string.IsNullOrWhiteSpace(configuredRoadLayer)
            ? "all layers"
            : configuredRoadLayer;

        var layers = new List<GisIntelligenceLayerCoverageDto>
        {
            BuildLayer(
                "administrative_verification",
                "Administrative verification",
                totalParcelCount,
                districtBoundaryCount,
                adminParcelEvidence,
                "Derived from land_intelligence.gis_administrative_boundaries (province/district polygons) during GIS enrichment.",
                "District boundary reference layer not imported in this database."),
            BuildLayer(
                "road_accessibility",
                "Road accessibility",
                totalParcelCount,
                roadReferenceCount,
                roadParcelEvidence,
                $"Nearest-road evidence from land_intelligence.gis_roads (configured road layer: {roadLayerLabel}).",
                "Road reference layer not imported in this database."),
            BuildLayer(
                "water_proximity",
                "Water proximity",
                totalParcelCount,
                waterReferenceCount,
                waterParcelEvidence,
                "Nearest natural water evidence from land_intelligence.gis_water_features.",
                "Water feature reference layer not imported in this database."),
            BuildLayer(
                "gis_soil_group",
                "GIS soil group",
                totalParcelCount,
                soilReferenceCount,
                soilParcelEvidence,
                "Overlap evidence from land_intelligence.gis_soil_groups persisted to parcel_derived_soil_groups.",
                "Soil group reference layer not imported in this database."),
            BuildLayer(
                "conservation_intersections",
                "Conservation intersections",
                totalParcelCount,
                conservationReferenceCount,
                conservationParcelEvidence,
                "Intersection evidence from land_intelligence.gis_soil_conservation_areas stored as GIS-derived environmental restrictions.",
                "Soil conservation area reference layer not imported in this database."),
            BuildLayer(
                "erosion_observations",
                "Erosion observations",
                totalParcelCount,
                erosionReferenceCount,
                erosionParcelEvidence,
                "Proximity/intersection from land_intelligence.gis_soil_erosion_observations during environmental enrichment (not separately persisted per parcel when absent).",
                "Soil erosion observation reference layer not imported in this database."),
        };

        return new LandIntelligenceGisCoverageDto(
            totalParcelCount,
            indexedDistricts,
            configuredDistricts,
            enrichmentSnapshotCount,
            completeCount,
            partialCount,
            unavailableCount,
            overallStatus,
            overallNotice,
            layers,
            measuredAt);
    }

    public async Task<ParcelGisEnrichmentSummaryDto> GetParcelEnrichmentSummaryAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.LandParcelGisEnrichmentSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.LandParcelId == landParcelId, cancellationToken);

        if (snapshot is null)
        {
            return new ParcelGisEnrichmentSummaryDto(
                GisIntelligenceCoverageStatus.Unavailable,
                null,
                null,
                null,
                "GIS enrichment has not been run for this parcel in the connected database. Spatial recommendation criteria treat missing GIS as not assessed.");
        }

        var status = snapshot.OverallStatus switch
        {
            LandParcelGisEnrichmentOverallStatus.Complete => GisIntelligenceCoverageStatus.Available,
            LandParcelGisEnrichmentOverallStatus.Partial => GisIntelligenceCoverageStatus.Partial,
            _ => GisIntelligenceCoverageStatus.Unavailable,
        };

        var notice = status switch
        {
            GisIntelligenceCoverageStatus.Available =>
                "Unified GIS enrichment snapshot is complete for this parcel. Evidence is derived from imported reference layers only.",
            GisIntelligenceCoverageStatus.Partial =>
                "Unified GIS enrichment snapshot is partial—some layers were unavailable or outside configured district coverage.",
            _ => "Unified GIS enrichment snapshot is unavailable for this parcel.",
        };

        return new ParcelGisEnrichmentSummaryDto(
            status,
            snapshot.DetectedProvince,
            snapshot.DetectedDistrict,
            snapshot.EnrichedAt,
            notice);
    }

    private async Task<int> CountDistinctParcelsWithGisDerivedInfrastructureAsync(
        InfrastructureFeatureType type,
        CancellationToken cancellationToken)
    {
        return await _dbContext.InfrastructureFeatures
            .AsNoTracking()
            .Where(feature => feature.Type == type && feature.GisReferenceId != null)
            .Select(feature => feature.LandParcelId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private async Task<int> CountDistinctParcelsWithGisDerivedEnvironmentalAsync(
        EnvironmentalRestrictionType type,
        CancellationToken cancellationToken)
    {
        return await _dbContext.EnvironmentalRestrictions
            .AsNoTracking()
            .Where(restriction => restriction.Type == type && restriction.GisReferenceId != null)
            .Select(restriction => restriction.LandParcelId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static GisIntelligenceLayerCoverageDto BuildLayer(
        string layerKey,
        string displayName,
        int totalParcelCount,
        int referenceFeatureCount,
        int parcelsWithEvidenceCount,
        string provenanceSummary,
        string missingReferenceNote)
    {
        var status = GisIntelligenceLayerCoverageClassifier.ClassifyLayer(
            totalParcelCount,
            referenceFeatureCount,
            parcelsWithEvidenceCount);

        var limitation = GisIntelligenceLayerCoverageClassifier.BuildLayerLimitation(
            status,
            totalParcelCount,
            referenceFeatureCount,
            parcelsWithEvidenceCount,
            missingReferenceNote);

        if (status == GisIntelligenceCoverageStatus.Unavailable
            && referenceFeatureCount > 0
            && parcelsWithEvidenceCount <= 0)
        {
            provenanceSummary += " Reference rows exist but no parcel-level evidence was persisted.";
        }

        return new GisIntelligenceLayerCoverageDto(
            layerKey,
            displayName,
            status,
            referenceFeatureCount,
            parcelsWithEvidenceCount,
            provenanceSummary,
            limitation);
    }
}

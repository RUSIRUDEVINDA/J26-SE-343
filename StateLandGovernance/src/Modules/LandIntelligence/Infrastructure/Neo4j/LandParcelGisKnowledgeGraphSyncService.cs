using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

public sealed class LandParcelGisKnowledgeGraphSyncService : ILandParcelGisKnowledgeGraphSyncService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly IKnowledgeGraphService _knowledgeGraphService;
    private readonly ILogger<LandParcelGisKnowledgeGraphSyncService> _logger;

    public LandParcelGisKnowledgeGraphSyncService(
        LandIntelligenceDbContext dbContext,
        IKnowledgeGraphService knowledgeGraphService,
        ILogger<LandParcelGisKnowledgeGraphSyncService> logger)
    {
        _dbContext = dbContext;
        _knowledgeGraphService = knowledgeGraphService;
        _logger = logger;
    }

    public async Task SyncAsync(Guid parcelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = await BuildSyncRequestAsync(parcelId, cancellationToken);
            if (request is null)
            {
                throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");
            }

            await _knowledgeGraphService.SyncGisDerivedParcelIntelligenceAsync(request, cancellationToken);

            _logger.LogInformation(
                "Synchronized GIS-derived intelligence for parcel {ParcelId} to the knowledge graph with status {OverallStatus}.",
                parcelId,
                request.OverallStatus);
        }
        catch (ServiceConfigurationException ex)
        {
            _logger.LogWarning(
                ex,
                "GIS knowledge graph synchronization skipped for parcel {ParcelId} because Neo4j is not configured.",
                parcelId);
        }
        catch (Neo4jException ex)
        {
            _logger.LogWarning(
                ex,
                "GIS knowledge graph synchronization failed for parcel {ParcelId} because Neo4j is unavailable.",
                parcelId);
        }
    }

    internal async Task<GisDerivedParcelIntelligenceGraphSyncRequest?> BuildSyncRequestAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var parcel = await _dbContext.LandParcels
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == parcelId, cancellationToken);

        if (parcel is null)
        {
            return null;
        }

        var snapshot = await _dbContext.LandParcelGisEnrichmentSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.LandParcelId == parcelId, cancellationToken);

        if (snapshot is null)
        {
            return new GisDerivedParcelIntelligenceGraphSyncRequest
            {
                ParcelId = parcelId,
                CadastralNumber = parcel.CadastralNumber,
                SurveyPlanReference = parcel.SurveyPlanReference,
                OverallStatus = LandParcelGisEnrichmentOverallStatus.Unavailable,
                ConservationAreas = []
            };
        }

        var infrastructureFeatures = await _dbContext.InfrastructureFeatures
            .AsNoTracking()
            .Where(feature => feature.LandParcelId == parcelId)
            .ToListAsync(cancellationToken);
        var environmentalRestrictions = await _dbContext.EnvironmentalRestrictions
            .AsNoTracking()
            .Where(restriction => restriction.LandParcelId == parcelId)
            .ToListAsync(cancellationToken);
        var derivedSoil = await _dbContext.ParcelDerivedSoilGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.LandParcelId == parcelId, cancellationToken);

        var gisRoad = infrastructureFeatures
            .FirstOrDefault(feature =>
                feature.Type == InfrastructureFeatureType.Road
                && LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedInfrastructure(feature));
        var gisWater = infrastructureFeatures
            .FirstOrDefault(feature =>
                feature.Type == InfrastructureFeatureType.Other
                && LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedInfrastructure(feature));
        var gisConservation = environmentalRestrictions
            .Where(restriction =>
                restriction.Type == EnvironmentalRestrictionType.ProtectedArea
                && LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedEnvironmentalRestriction(restriction))
            .ToList();

        GisDerivedAdministrativeGraphSync? administrative = null;
        GisDerivedRoadGraphSync? road = null;
        GisDerivedWaterGraphSync? water = null;
        GisDerivedSoilGraphSync? soil = null;
        IReadOnlyList<GisDerivedConservationGraphSync> conservationAreas = [];

        if (snapshot.OverallStatus != LandParcelGisEnrichmentOverallStatus.Unavailable)
        {
            administrative = await BuildAdministrativeSyncAsync(snapshot, cancellationToken);
            road = await BuildRoadSyncAsync(gisRoad, snapshot.EnrichedAt, cancellationToken);
            water = await BuildWaterSyncAsync(gisWater, snapshot.EnrichedAt, cancellationToken);
            soil = BuildSoilSync(derivedSoil);
            conservationAreas = await BuildConservationSyncAsync(gisConservation, snapshot.EnrichedAt, cancellationToken);
        }

        return new GisDerivedParcelIntelligenceGraphSyncRequest
        {
            ParcelId = parcelId,
            CadastralNumber = parcel.CadastralNumber,
            SurveyPlanReference = parcel.SurveyPlanReference,
            OverallStatus = snapshot.OverallStatus,
            Administrative = administrative,
            Road = road,
            Water = water,
            Soil = soil,
            ConservationAreas = conservationAreas
        };
    }

    private async Task<GisDerivedAdministrativeGraphSync?> BuildAdministrativeSyncAsync(
        Persistence.Entities.LandParcelGisEnrichmentSnapshotEntity snapshot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshot.DetectedProvince)
            || string.IsNullOrWhiteSpace(snapshot.DetectedDistrict))
        {
            return null;
        }

        var province = await _dbContext.GisAdministrativeBoundaries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                boundary =>
                    boundary.Name == snapshot.DetectedProvince
                    && boundary.BoundaryType == GisAdministrativeBoundaryType.Province,
                cancellationToken);

        var district = await _dbContext.GisAdministrativeBoundaries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                boundary =>
                    boundary.Name == snapshot.DetectedDistrict
                    && boundary.BoundaryType == GisAdministrativeBoundaryType.District,
                cancellationToken);

        if (province is null || district is null)
        {
            return null;
        }

        return new GisDerivedAdministrativeGraphSync(
            province.Id,
            province.Name,
            district.Id,
            district.Name,
            snapshot.SourceName,
            snapshot.EnrichedAt);
    }

    private async Task<GisDerivedRoadGraphSync?> BuildRoadSyncAsync(
        Persistence.Entities.InfrastructureFeatureEntity? feature,
        DateTimeOffset derivedAt,
        CancellationToken cancellationToken)
    {
        if (feature?.GisReferenceId is null || feature.DistanceMeters is null)
        {
            return null;
        }

        var road = await _dbContext.GisRoads
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == feature.GisReferenceId, cancellationToken);

        return new GisDerivedRoadGraphSync(
            feature.GisReferenceId.Value,
            road?.Name ?? feature.Name,
            road?.RoadType.ToString() ?? "Unspecified",
            feature.DistanceMeters.Value,
            road?.SourceName ?? GisDerivedIntelligenceOwnership.SourceName,
            ReadDerivedAt(feature.DistanceProvenanceJson) ?? derivedAt);
    }

    private async Task<GisDerivedWaterGraphSync?> BuildWaterSyncAsync(
        Persistence.Entities.InfrastructureFeatureEntity? feature,
        DateTimeOffset derivedAt,
        CancellationToken cancellationToken)
    {
        if (feature?.GisReferenceId is null || feature.DistanceMeters is null)
        {
            return null;
        }

        var water = await _dbContext.GisWaterFeatures
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == feature.GisReferenceId, cancellationToken);

        return new GisDerivedWaterGraphSync(
            feature.GisReferenceId.Value,
            water?.Name ?? feature.Name,
            water?.FeatureType.ToString() ?? "Unspecified",
            feature.DistanceMeters.Value,
            water?.SourceName ?? GisDerivedIntelligenceOwnership.SourceName,
            ReadDerivedAt(feature.DistanceProvenanceJson) ?? derivedAt);
    }

    private static GisDerivedSoilGraphSync? BuildSoilSync(
        Persistence.Entities.ParcelDerivedSoilGroupEntity? derivedSoil)
    {
        if (derivedSoil is null)
        {
            return null;
        }

        return new GisDerivedSoilGraphSync(
            derivedSoil.SoilGroupReferenceId,
            derivedSoil.SoilGroupName,
            derivedSoil.OverlapPercentage,
            derivedSoil.SourceName,
            ReadDerivedAt(derivedSoil.ProvenanceJson) ?? derivedSoil.DerivedAt);
    }

    private async Task<IReadOnlyList<GisDerivedConservationGraphSync>> BuildConservationSyncAsync(
        IReadOnlyList<Persistence.Entities.EnvironmentalRestrictionEntity> restrictions,
        DateTimeOffset derivedAt,
        CancellationToken cancellationToken)
    {
        if (restrictions.Count == 0)
        {
            return [];
        }

        var referenceIds = restrictions
            .Where(restriction => restriction.GisReferenceId is not null)
            .Select(restriction => restriction.GisReferenceId!.Value)
            .ToList();

        var conservationAreas = await _dbContext.GisSoilConservationAreas
            .AsNoTracking()
            .Where(area => referenceIds.Contains(area.Id))
            .ToDictionaryAsync(area => area.Id, cancellationToken);

        var results = new List<GisDerivedConservationGraphSync>();

        foreach (var restriction in restrictions.Where(item => item.GisReferenceId is not null))
        {
            conservationAreas.TryGetValue(restriction.GisReferenceId!.Value, out var area);
            results.Add(new GisDerivedConservationGraphSync(
                restriction.GisReferenceId.Value,
                area?.Name ?? restriction.Description,
                ParseOverlapPercentage(restriction.Description),
                area?.SourceName ?? GisDerivedIntelligenceOwnership.SourceName,
                ReadDerivedAt(restriction.DataProvenanceJson) ?? derivedAt));
        }

        return results;
    }

    private static DateTimeOffset? ReadDerivedAt(string? provenanceJson)
    {
        var provenance = AttributeProvenancePersistenceMapper.Deserialize(provenanceJson);
        return provenance?.CollectedAt;
    }

    private static decimal? ParseOverlapPercentage(string description)
    {
        var start = description.IndexOf('(');
        var end = description.IndexOf('%');
        if (start < 0 || end <= start)
        {
            return null;
        }

        var value = description[(start + 1)..end].Trim();
        return decimal.TryParse(value, out var parsed) ? parsed : null;
    }
}

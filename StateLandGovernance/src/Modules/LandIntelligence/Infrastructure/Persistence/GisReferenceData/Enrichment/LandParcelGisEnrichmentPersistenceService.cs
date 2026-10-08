using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

public sealed class LandParcelGisEnrichmentPersistenceService : ILandParcelGisEnrichmentPersistenceService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILogger<LandParcelGisEnrichmentPersistenceService> _logger;

    public LandParcelGisEnrichmentPersistenceService(
        LandIntelligenceDbContext dbContext,
        ILogger<LandParcelGisEnrichmentPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistAsync(
        LandParcelGisEnrichmentResult result,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var parcelExists = await _dbContext.LandParcels
                .AnyAsync(entity => entity.Id == result.ParcelId, cancellationToken);

            if (!parcelExists)
            {
                throw new KeyNotFoundException($"Land parcel '{result.ParcelId}' was not found.");
            }

            await SyncGisDerivedInfrastructureFeaturesAsync(result, cancellationToken);
            await SyncGisDerivedEnvironmentalRestrictionsAsync(result, cancellationToken);

            if (result.OverallStatus != LandParcelGisEnrichmentOverallStatus.Unavailable)
            {
                await UpsertDerivedSoilGroupAsync(result, cancellationToken);
            }
            else
            {
                await RemoveDerivedSoilGroupAsync(result.ParcelId, cancellationToken);
            }

            await UpsertSnapshotAsync(result, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Persisted GIS-derived intelligence for parcel {ParcelId} with overall status {OverallStatus}.",
                result.ParcelId,
                result.OverallStatus);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                ex,
                "Failed to persist GIS-derived intelligence for parcel {ParcelId}.",
                result.ParcelId);
            throw;
        }
    }

    public async Task InvalidateLocationDependentEvidenceAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var parcelExists = await _dbContext.LandParcels
                .AnyAsync(entity => entity.Id == parcelId, cancellationToken);
            if (!parcelExists)
            {
                throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");
            }

            var gisInfrastructure = await _dbContext.InfrastructureFeatures
                .Where(feature => feature.LandParcelId == parcelId)
                .ToListAsync(cancellationToken);
            foreach (var feature in gisInfrastructure.Where(LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedInfrastructure))
            {
                _dbContext.InfrastructureFeatures.Remove(feature);
            }

            var gisRestrictions = await _dbContext.EnvironmentalRestrictions
                .Where(restriction => restriction.LandParcelId == parcelId)
                .ToListAsync(cancellationToken);
            foreach (var restriction in gisRestrictions.Where(
                         LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedEnvironmentalRestriction))
            {
                _dbContext.EnvironmentalRestrictions.Remove(restriction);
            }

            await RemoveDerivedSoilGroupAsync(parcelId, cancellationToken);

            var snapshot = await _dbContext.LandParcelGisEnrichmentSnapshots
                .FirstOrDefaultAsync(entity => entity.LandParcelId == parcelId, cancellationToken);
            if (snapshot is not null)
            {
                // Mark stale until enrichment is re-run; do not invent replacement distances.
                snapshot.OverallStatus = LandParcelGisEnrichmentOverallStatus.Unavailable;
                snapshot.AdministrativeStatus = null;
                snapshot.DetectedProvince = null;
                snapshot.DetectedDistrict = null;
                snapshot.ProvinceMatches = null;
                snapshot.DistrictMatches = null;
                snapshot.GeometryBasis = null;
                snapshot.SourceName = GisDerivedIntelligenceOwnership.SourceName;
                snapshot.EnrichedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Invalidated location-dependent GIS evidence for parcel {ParcelId} after spatial change.",
                parcelId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                ex,
                "Failed to invalidate location-dependent GIS evidence for parcel {ParcelId}.",
                parcelId);
            throw;
        }
    }

    private async Task SyncGisDerivedInfrastructureFeaturesAsync(
        LandParcelGisEnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var desired = new List<InfrastructureFeatureEntity>();
        var roadFeature = LandParcelGisEnrichmentPersistenceMapper.ToRoadInfrastructureEntity(result);
        if (roadFeature is not null)
        {
            desired.Add(roadFeature);
        }

        var waterFeature = LandParcelGisEnrichmentPersistenceMapper.ToWaterInfrastructureEntity(result);
        if (waterFeature is not null)
        {
            desired.Add(waterFeature);
        }

        var desiredIds = desired.Select(feature => feature.Id).ToHashSet();
        var existingGisOwned = await _dbContext.InfrastructureFeatures
            .Where(feature => feature.LandParcelId == result.ParcelId)
            .ToListAsync(cancellationToken);
        existingGisOwned = existingGisOwned
            .Where(LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedInfrastructure)
            .ToList();

        foreach (var existing in existingGisOwned.Where(feature => !desiredIds.Contains(feature.Id)))
        {
            _dbContext.InfrastructureFeatures.Remove(existing);
        }

        foreach (var target in desired)
        {
            var existing = existingGisOwned.FirstOrDefault(feature => feature.Id == target.Id);
            if (existing is null)
            {
                _dbContext.InfrastructureFeatures.Add(target);
                continue;
            }

            existing.Type = target.Type;
            existing.Name = target.Name;
            existing.DistanceMeters = target.DistanceMeters;
            existing.Description = target.Description;
            existing.DistanceProvenanceJson = target.DistanceProvenanceJson;
        }
    }

    private async Task SyncGisDerivedEnvironmentalRestrictionsAsync(
        LandParcelGisEnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var desired = LandParcelGisEnrichmentPersistenceMapper.ToConservationRestrictionEntities(result);
        var desiredIds = desired.Select(restriction => restriction.Id).ToHashSet();
        var existingGisOwned = await _dbContext.EnvironmentalRestrictions
            .Where(restriction => restriction.LandParcelId == result.ParcelId)
            .ToListAsync(cancellationToken);
        existingGisOwned = existingGisOwned
            .Where(LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedEnvironmentalRestriction)
            .ToList();

        foreach (var existing in existingGisOwned.Where(restriction => !desiredIds.Contains(restriction.Id)))
        {
            _dbContext.EnvironmentalRestrictions.Remove(existing);
        }

        foreach (var target in desired)
        {
            var existing = existingGisOwned.FirstOrDefault(restriction => restriction.Id == target.Id);
            if (existing is null)
            {
                _dbContext.EnvironmentalRestrictions.Add(target);
                continue;
            }

            existing.Type = target.Type;
            existing.Description = target.Description;
            existing.Severity = target.Severity;
            existing.DataProvenanceJson = target.DataProvenanceJson;
        }
    }

    private async Task UpsertDerivedSoilGroupAsync(
        LandParcelGisEnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var derivedSoil = LandParcelGisEnrichmentPersistenceMapper.ToDerivedSoilGroupEntity(result);
        var existing = await _dbContext.ParcelDerivedSoilGroups
            .FirstOrDefaultAsync(entity => entity.LandParcelId == result.ParcelId, cancellationToken);

        if (derivedSoil is null)
        {
            if (existing is not null)
            {
                _dbContext.ParcelDerivedSoilGroups.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            _dbContext.ParcelDerivedSoilGroups.Add(derivedSoil);
            return;
        }

        existing.SoilGroupReferenceId = derivedSoil.SoilGroupReferenceId;
        existing.SoilGroupName = derivedSoil.SoilGroupName;
        existing.OverlapAreaSquareMeters = derivedSoil.OverlapAreaSquareMeters;
        existing.OverlapPercentage = derivedSoil.OverlapPercentage;
        existing.GeometryBasis = derivedSoil.GeometryBasis;
        existing.SourceName = derivedSoil.SourceName;
        existing.SourceLayer = derivedSoil.SourceLayer;
        existing.ProvenanceJson = derivedSoil.ProvenanceJson;
        existing.DerivedAt = derivedSoil.DerivedAt;
    }

    private async Task RemoveDerivedSoilGroupAsync(Guid parcelId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ParcelDerivedSoilGroups
            .FirstOrDefaultAsync(entity => entity.LandParcelId == parcelId, cancellationToken);

        if (existing is not null)
        {
            _dbContext.ParcelDerivedSoilGroups.Remove(existing);
        }
    }

    private async Task UpsertSnapshotAsync(
        LandParcelGisEnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var snapshot = LandParcelGisEnrichmentPersistenceMapper.ToSnapshotEntity(result);
        var existing = await _dbContext.LandParcelGisEnrichmentSnapshots
            .FirstOrDefaultAsync(entity => entity.LandParcelId == result.ParcelId, cancellationToken);

        if (existing is null)
        {
            _dbContext.LandParcelGisEnrichmentSnapshots.Add(snapshot);
            return;
        }

        existing.OverallStatus = snapshot.OverallStatus;
        existing.AdministrativeStatus = snapshot.AdministrativeStatus;
        existing.DetectedProvince = snapshot.DetectedProvince;
        existing.DetectedDistrict = snapshot.DetectedDistrict;
        existing.ProvinceMatches = snapshot.ProvinceMatches;
        existing.DistrictMatches = snapshot.DistrictMatches;
        existing.GeometryBasis = snapshot.GeometryBasis;
        existing.SourceName = snapshot.SourceName;
        existing.EnrichedAt = snapshot.EnrichedAt;
    }
}

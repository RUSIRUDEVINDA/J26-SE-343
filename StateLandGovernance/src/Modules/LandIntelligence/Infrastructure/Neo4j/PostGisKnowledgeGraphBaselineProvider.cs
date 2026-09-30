using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

internal sealed class PostGisKnowledgeGraphBaselineProvider : IPostGisKnowledgeGraphBaselineProvider
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly IGisGraphSyncRequestBuilder _syncRequestBuilder;

    public PostGisKnowledgeGraphBaselineProvider(
        LandIntelligenceDbContext dbContext,
        ILandParcelRepository landParcelRepository,
        IGisGraphSyncRequestBuilder syncRequestBuilder)
    {
        _dbContext = dbContext;
        _landParcelRepository = landParcelRepository;
        _syncRequestBuilder = syncRequestBuilder;
    }

    public async Task<LandParcelGisGraphIntelligenceDto?> GetGisGraphIntelligenceAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var syncRequest = await _syncRequestBuilder.BuildSyncRequestAsync(parcelId, cancellationToken);
        return syncRequest is null
            ? null
            : PostGisKnowledgeGraphBaselineMapper.ToGisGraphIntelligence(syncRequest);
    }

    public async Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _landParcelRepository.GetByIdAsync(parcelId, cancellationToken);
        if (parcel is null)
        {
            return [];
        }

        var syncRequest = await _syncRequestBuilder.BuildSyncRequestAsync(parcelId, cancellationToken);
        return PostGisKnowledgeGraphBaselineMapper.ToRelationships(parcel, syncRequest);
    }

    public async Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.LandCategoryId == categoryId)
            .Select(parcel => parcel.Id)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(
        Guid soilGroupReferenceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ParcelDerivedSoilGroups
            .AsNoTracking()
            .Where(entity => entity.SoilGroupReferenceId == soilGroupReferenceId)
            .Select(entity => entity.LandParcelId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class ParcelShortlistService : IParcelShortlistService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILandIntelligenceCallerContext _caller;
    private readonly ILandParcelRepository _parcelRepository;
    private readonly TimeProvider _timeProvider;

    public ParcelShortlistService(
        LandIntelligenceDbContext dbContext,
        ILandIntelligenceCallerContext caller,
        ILandParcelRepository parcelRepository,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _caller = caller;
        _parcelRepository = parcelRepository;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ParcelShortlistItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();

        var items = await _dbContext.ParcelShortlistItems
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.SavedAtUtc)
            .ToListAsync(cancellationToken);

        var result = new List<ParcelShortlistItemDto>();
        foreach (var item in items)
        {
            var parcel = await _parcelRepository.GetByIdAsync(item.LandParcelId, cancellationToken);
            result.Add(new ParcelShortlistItemDto(
                item.LandParcelId,
                item.SavedAtUtc,
                parcel?.Identifier.CadastralNumber,
                parcel?.Location.District,
                parcel is null ? null : parcel.Area.ToHectares()));
        }

        return result;
    }

    public async Task SaveAsync(Guid landParcelId, CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();

        if (await _parcelRepository.GetByIdAsync(landParcelId, cancellationToken) is null)
        {
            throw new LandParcelNotFoundException(landParcelId);
        }

        var existing = await _dbContext.ParcelShortlistItems
            .FirstOrDefaultAsync(
                x => x.OwnerId == ownerId && x.LandParcelId == landParcelId,
                cancellationToken);

        var now = _timeProvider.GetUtcNow();
        if (existing is null)
        {
            _dbContext.ParcelShortlistItems.Add(new ParcelShortlistItemEntity
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                LandParcelId = landParcelId,
                SavedAtUtc = now,
            });
        }
        else
        {
            existing.SavedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid landParcelId, CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();

        var existing = await _dbContext.ParcelShortlistItems
            .FirstOrDefaultAsync(
                x => x.OwnerId == ownerId && x.LandParcelId == landParcelId,
                cancellationToken);

        if (existing is null)
        {
            return;
        }

        _dbContext.ParcelShortlistItems.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireOwnerId()
    {
        if (!_caller.IsAuthenticated)
        {
            throw new LandIntelligenceAuthorizationException(
                "An owner identity is required. Send a valid X-Land-Intelligence-Owner-Id header.");
        }

        return _caller.OwnerId!.Value;
    }
}

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class ApprovedPreliminaryLeaseParcelRegistry : IApprovedPreliminaryLeaseParcelRegistry
{
    private const int MaxPageSize = 100;

    private readonly LandIntelligenceDbContext _dbContext;
    private readonly IRealParcelSourceReadRepository _realParcelSources;

    public ApprovedPreliminaryLeaseParcelRegistry(
        LandIntelligenceDbContext dbContext,
        IRealParcelSourceReadRepository realParcelSources)
    {
        _dbContext = dbContext;
        _realParcelSources = realParcelSources;
    }

    public async Task<IReadOnlySet<Guid>> GetApprovedParcelIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(request => request.Status == LeaseInterestRequestStatus.ApprovedInPrinciple)
            .Select(request => request.LandParcelId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<bool> HasApprovedPreliminaryRequestAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .AnyAsync(
                request =>
                    request.LandParcelId == landParcelId
                    && request.Status == LeaseInterestRequestStatus.ApprovedInPrinciple,
                cancellationToken);

    public async Task<ApprovedPreliminaryParcelListResponse> ListApprovedParcelsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var approvedRequests = await _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(request => request.Status == LeaseInterestRequestStatus.ApprovedInPrinciple)
            .ToListAsync(cancellationToken);

        var representativeByParcel = approvedRequests
            .GroupBy(request => request.LandParcelId)
            .Select(group => group.OrderByDescending(request => request.UpdatedAtUtc).First())
            .ToList();

        var requestIds = representativeByParcel.Select(request => request.Id).ToList();
        var decisions = await _dbContext.LeaseInterestAuthorityDecisions
            .AsNoTracking()
            .Where(decision =>
                requestIds.Contains(decision.LeaseInterestRequestId)
                && decision.Decision == LeaseInterestAuthorityDecisionKind.Approved)
            .ToListAsync(cancellationToken);

        var approvalAtByRequest = decisions
            .GroupBy(decision => decision.LeaseInterestRequestId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(decision => decision.RecordedAtUtc));

        var ordered = representativeByParcel
            .Select(request =>
            {
                var approvedAt = approvalAtByRequest.TryGetValue(request.Id, out var recorded)
                    ? recorded
                    : request.UpdatedAtUtc;
                return new { Request = request, ApprovedAt = approvedAt };
            })
            .OrderByDescending(row => row.ApprovedAt)
            .ThenBy(row => row.Request.LandParcelId)
            .ToList();

        var totalCount = ordered.Count;
        var pageRows = ordered
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        var parcelIds = pageRows.Select(row => row.Request.LandParcelId).ToList();
        var parcels = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(parcel => parcelIds.Contains(parcel.Id))
            .ToDictionaryAsync(parcel => parcel.Id, cancellationToken);

        var sources = await _realParcelSources.GetByParcelIdsAsync(parcelIds, cancellationToken);

        var results = new List<ApprovedPreliminaryParcelSummaryDto>(pageRows.Count);
        foreach (var row in pageRows)
        {
            if (!parcels.TryGetValue(row.Request.LandParcelId, out var parcel))
            {
                continue;
            }

            sources.TryGetValue(parcel.Id, out var source);
            var landUse = source?.LandUseEnglishLabel?.Trim();
            if (string.IsNullOrWhiteSpace(landUse))
            {
                landUse = source?.LandUseCode?.Trim();
            }

            results.Add(new ApprovedPreliminaryParcelSummaryDto(
                parcel.Id,
                parcel.CadastralNumber,
                parcel.Province,
                parcel.District,
                parcel.AreaValue,
                parcel.AreaUnit,
                string.IsNullOrWhiteSpace(landUse) ? null : landUse,
                row.ApprovedAt,
                row.Request.Id,
                row.Request.AcknowledgementReference));
        }

        return new ApprovedPreliminaryParcelListResponse(
            results,
            safePage,
            safePageSize,
            totalCount);
    }
}

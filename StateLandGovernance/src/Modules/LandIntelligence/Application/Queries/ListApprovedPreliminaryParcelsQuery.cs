using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed record ListApprovedPreliminaryParcelsQuery(int Page = 1, int PageSize = 20);

public sealed class ListApprovedPreliminaryParcelsQueryHandler
    : IQueryHandler<ListApprovedPreliminaryParcelsQuery, ApprovedPreliminaryParcelListResponse>
{
    private readonly IApprovedPreliminaryLeaseParcelRegistry _registry;

    public ListApprovedPreliminaryParcelsQueryHandler(
        IApprovedPreliminaryLeaseParcelRegistry registry)
    {
        _registry = registry;
    }

    public Task<ApprovedPreliminaryParcelListResponse> HandleAsync(
        ListApprovedPreliminaryParcelsQuery query,
        CancellationToken cancellationToken = default) =>
        _registry.ListApprovedParcelsAsync(query.Page, query.PageSize, cancellationToken);
}

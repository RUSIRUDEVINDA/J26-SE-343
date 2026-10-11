using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetGovernanceDashboardQuery;

public sealed class GetGovernanceDashboardQueryHandler
{
    private readonly IGovernanceDashboardReader _reader;

    public GetGovernanceDashboardQueryHandler(IGovernanceDashboardReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public Task<GovernanceDashboardDto> HandleAsync(
        GetGovernanceDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _reader.GetDashboardAsync(cancellationToken);
    }
}


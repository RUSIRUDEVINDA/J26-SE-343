using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetEarlyGovernanceReferralHistoryQuery(string CaseId, int Limit = 100);

public sealed class GetEarlyGovernanceReferralHistoryQueryHandler
{
    private readonly IEarlyGovernanceScreeningStore _store;

    public GetEarlyGovernanceReferralHistoryQueryHandler(IEarlyGovernanceScreeningStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<StoredEarlyGovernanceReferralDto>> HandleAsync(
        GetEarlyGovernanceReferralHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.CaseId))
        {
            throw new ArgumentException("CaseId cannot be blank.", nameof(query));
        }

        if (query.Limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Limit must be between 1 and 200.");
        }

        return _store.GetReferralHistoryByCaseIdAsync(query.CaseId, query.Limit, cancellationToken);
    }
}

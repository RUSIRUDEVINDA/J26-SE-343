using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetEarlyGovernanceScreeningAssessmentQuery(Guid AssessmentId);

public sealed class GetEarlyGovernanceScreeningAssessmentQueryHandler
{
    private readonly IEarlyGovernanceScreeningStore _store;

    public GetEarlyGovernanceScreeningAssessmentQueryHandler(IEarlyGovernanceScreeningStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<StoredEarlyGovernanceScreeningDto?> HandleAsync(
        GetEarlyGovernanceScreeningAssessmentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.AssessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(query));
        }

        return _store.GetByIdAsync(query.AssessmentId, cancellationToken);
    }
}

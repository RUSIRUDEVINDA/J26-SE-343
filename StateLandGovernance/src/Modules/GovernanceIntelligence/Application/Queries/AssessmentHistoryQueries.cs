using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetEarlyGovernanceScreeningHistoryQuery(
    string CaseId,
    int Limit = IEarlyGovernanceScreeningStore.DefaultHistoryLimit);

public sealed class GetEarlyGovernanceScreeningHistoryQueryHandler
{
    private readonly IEarlyGovernanceScreeningStore _store;

    public GetEarlyGovernanceScreeningHistoryQueryHandler(IEarlyGovernanceScreeningStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<StoredEarlyGovernanceScreeningDto>> HandleAsync(
        GetEarlyGovernanceScreeningHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _store.GetAssessmentHistoryByCaseIdAsync(query.CaseId, query.Limit, cancellationToken);
    }
}

public sealed record GetComplaintClassificationAssessmentQuery(Guid AssessmentId);

public sealed class GetComplaintClassificationAssessmentQueryHandler
{
    private readonly IComplaintClassificationAssessmentStore _store;

    public GetComplaintClassificationAssessmentQueryHandler(IComplaintClassificationAssessmentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<ComplaintClassificationAssessment?> HandleAsync(
        GetComplaintClassificationAssessmentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.AssessmentId == Guid.Empty) throw new ArgumentException("AssessmentId cannot be empty.", nameof(query));
        return _store.GetByIdAsync(query.AssessmentId, cancellationToken);
    }
}

public sealed record GetComplaintClassificationHistoryQuery(
    string CaseId,
    int Limit = IComplaintClassificationAssessmentStore.DefaultHistoryLimit);

public sealed class GetComplaintClassificationHistoryQueryHandler
{
    private readonly IComplaintClassificationAssessmentStore _store;

    public GetComplaintClassificationHistoryQueryHandler(IComplaintClassificationAssessmentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<ComplaintClassificationAssessment>> HandleAsync(
        GetComplaintClassificationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _store.GetByCaseIdAsync(query.CaseId, query.Limit, cancellationToken);
    }
}

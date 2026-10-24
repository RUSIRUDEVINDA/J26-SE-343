using StateLandGovernance.GovernanceIntelligence.Application.Timing;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetLeaseApprovalTimingAssessmentQuery(Guid AssessmentId);
public sealed record GetLeaseApprovalTimingHistoryByCaseQuery(string CaseId, int Limit = ILeaseApprovalTimingAssessmentStore.DefaultHistoryLimit);
public sealed record GetLeaseApprovalTimingHistoryByWorkflowRunQuery(Guid WorkflowRunId, int Limit = ILeaseApprovalTimingAssessmentStore.DefaultHistoryLimit);

public sealed class GetLeaseApprovalTimingAssessmentQueryHandler
{
    private readonly ILeaseApprovalTimingAssessmentStore _store;
    public GetLeaseApprovalTimingAssessmentQueryHandler(ILeaseApprovalTimingAssessmentStore store) => _store = store;
    public Task<LeaseApprovalTimingAssessment?> HandleAsync(GetLeaseApprovalTimingAssessmentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.AssessmentId == Guid.Empty) throw new ArgumentException("AssessmentId cannot be empty.", nameof(query));
        return _store.GetByIdAsync(query.AssessmentId, cancellationToken);
    }
}

public sealed class GetLeaseApprovalTimingHistoryByCaseQueryHandler
{
    private readonly ILeaseApprovalTimingAssessmentStore _store;
    public GetLeaseApprovalTimingHistoryByCaseQueryHandler(ILeaseApprovalTimingAssessmentStore store) => _store = store;
    public Task<IReadOnlyList<LeaseApprovalTimingAssessment>> HandleAsync(GetLeaseApprovalTimingHistoryByCaseQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _store.GetByCaseIdAsync(query.CaseId, query.Limit, cancellationToken);
    }
}

public sealed class GetLeaseApprovalTimingHistoryByWorkflowRunQueryHandler
{
    private readonly ILeaseApprovalTimingAssessmentStore _store;
    public GetLeaseApprovalTimingHistoryByWorkflowRunQueryHandler(ILeaseApprovalTimingAssessmentStore store) => _store = store;
    public Task<IReadOnlyList<LeaseApprovalTimingAssessment>> HandleAsync(GetLeaseApprovalTimingHistoryByWorkflowRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _store.GetByWorkflowRunIdAsync(query.WorkflowRunId, query.Limit, cancellationToken);
    }
}

using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

/// <summary>
/// Retrieves a single persisted workflow-anomaly assessment by its <see cref="AssessmentId"/>.
/// </summary>
public sealed record GetWorkflowAnomalyAssessmentQuery(Guid AssessmentId);

public sealed class GetWorkflowAnomalyAssessmentQueryHandler
{
    private readonly IWorkflowAnomalyAssessmentStore _store;

    public GetWorkflowAnomalyAssessmentQueryHandler(IWorkflowAnomalyAssessmentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<WorkflowAnomalyAssessment?> HandleAsync(
        GetWorkflowAnomalyAssessmentQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        if (query.AssessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(query));
        }

        return _store.GetByIdAsync(query.AssessmentId, cancellationToken);
    }
}
/// <summary>
/// Retrieves bounded newest-first workflow-anomaly assessment history for a Case ID.
/// </summary>
public sealed record GetWorkflowAnomalyHistoryByCaseQuery(
    string CaseId,
    int Limit = IWorkflowAnomalyAssessmentStore.DefaultHistoryLimit);

public sealed class GetWorkflowAnomalyHistoryByCaseQueryHandler
{
    private readonly IWorkflowAnomalyAssessmentStore _store;

    public GetWorkflowAnomalyHistoryByCaseQueryHandler(IWorkflowAnomalyAssessmentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<WorkflowAnomalyAssessment>> HandleAsync(
        GetWorkflowAnomalyHistoryByCaseQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.CaseId))
        {
            throw new ArgumentException("CaseId cannot be blank.", nameof(query));
        }

        if (query.Limit is < 1 or > IWorkflowAnomalyAssessmentStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Limit must be between 1 and {IWorkflowAnomalyAssessmentStore.MaxHistoryLimit}.");
        }

        return _store.GetByCaseIdAsync(query.CaseId, query.Limit, cancellationToken);
    }
}

/// <summary>
/// Retrieves bounded newest-first workflow-anomaly assessment history for a Workflow Run ID.
/// </summary>
public sealed record GetWorkflowAnomalyHistoryByWorkflowRunQuery(
    Guid WorkflowRunId,
    int Limit = IWorkflowAnomalyAssessmentStore.DefaultHistoryLimit);

public sealed class GetWorkflowAnomalyHistoryByWorkflowRunQueryHandler
{
    private readonly IWorkflowAnomalyAssessmentStore _store;

    public GetWorkflowAnomalyHistoryByWorkflowRunQueryHandler(IWorkflowAnomalyAssessmentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<WorkflowAnomalyAssessment>> HandleAsync(
        GetWorkflowAnomalyHistoryByWorkflowRunQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        if (query.WorkflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(query));
        }

        if (query.Limit is < 1 or > IWorkflowAnomalyAssessmentStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Limit must be between 1 and {IWorkflowAnomalyAssessmentStore.MaxHistoryLimit}.");
        }

        return _store.GetByWorkflowRunIdAsync(query.WorkflowRunId, query.Limit, cancellationToken);
    }
}

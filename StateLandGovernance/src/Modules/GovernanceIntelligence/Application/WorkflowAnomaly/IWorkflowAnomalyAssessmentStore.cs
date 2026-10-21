namespace StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

/// <summary>
/// Persistence contract for workflow-anomaly assessments in Component 4.
/// Supports idempotent persistence by (WorkflowRunId, ModelVersion, InputSha256)
/// and bounded newest-first history retrieval.
/// </summary>
public interface IWorkflowAnomalyAssessmentStore
{
    public const int DefaultHistoryLimit = 100;
    public const int MaxHistoryLimit = 200;

    /// <summary>
    /// Persists a workflow-anomaly assessment idempotently.
    /// If an assessment with the same (WorkflowRunId, ModelVersion, InputSha256) already exists,
    /// returns the existing persisted assessment without creating a duplicate row.
    /// </summary>
    Task<WorkflowAnomalyAssessment> UpsertAsync(
        WorkflowAnomalyAssessment assessment,
        CancellationToken cancellationToken = default);

    Task<WorkflowAnomalyAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<WorkflowAnomalyAssessment?> GetByWorkflowRunModelAndHashAsync(
        Guid workflowRunId,
        string modelVersion,
        string inputSha256,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowAnomalyAssessment>> GetByWorkflowRunIdAsync(
        Guid workflowRunId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowAnomalyAssessment>> GetByCaseIdAsync(
        string caseId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);
}

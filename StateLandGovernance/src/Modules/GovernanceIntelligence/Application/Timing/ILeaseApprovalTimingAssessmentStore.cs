namespace StateLandGovernance.GovernanceIntelligence.Application.Timing;

/// <summary>
/// Persistence contract for lease-approval handover timing assessments in Component 4.
/// Supports idempotent persistence by (WorkflowRunId, PolicyVersion, InputSha256)
/// and bounded newest-first history retrieval.
/// </summary>
public interface ILeaseApprovalTimingAssessmentStore
{
    public const int DefaultHistoryLimit = 100;
    public const int MaxHistoryLimit = 200;

    /// <summary>
    /// Persists a lease-approval timing assessment idempotently.
    /// If an assessment with the same (WorkflowRunId, PolicyVersion, InputSha256) already exists,
    /// returns the existing persisted assessment without creating a duplicate row.
    /// </summary>
    Task<LeaseApprovalTimingAssessment> UpsertAsync(
        LeaseApprovalTimingAssessment assessment,
        CancellationToken cancellationToken = default);

    Task<LeaseApprovalTimingAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<LeaseApprovalTimingAssessment?> GetByWorkflowRunPolicyAndHashAsync(
        Guid workflowRunId,
        string policyVersion,
        string inputSha256,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaseApprovalTimingAssessment>> GetByWorkflowRunIdAsync(
        Guid workflowRunId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaseApprovalTimingAssessment>> GetByCaseIdAsync(
        string caseId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);
}

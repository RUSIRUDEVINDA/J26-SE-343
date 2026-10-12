namespace StateLandGovernance.GovernanceIntelligence.Application.Projections;

/// <summary>
/// Persistence contract for Component 3 workflow governance projections ingested into Component 4.
/// Supports idempotent ingestion by (WorkflowRunId, SourceRecordVersion, InputSha256).
/// </summary>
public interface IComponent3WorkflowGovernanceProjectionStore
{
    public const int DefaultHistoryLimit = 100;
    public const int MaxHistoryLimit = 200;

    /// <summary>
    /// Persists a Component 3 workflow governance projection idempotently.
    /// If a projection with the same (WorkflowRunId, Provenance.SourceRecordVersion, InputSha256) already exists,
    /// returns the existing persisted projection without creating a duplicate row.
    /// Throws <see cref="InvalidOperationException"/> if the same (WorkflowRunId, Provenance.SourceRecordVersion)
    /// already exists with a conflicting InputSha256.
    /// </summary>
    Task<Component3WorkflowGovernanceProjection> UpsertAsync(
        Component3WorkflowGovernanceProjection projection,
        CancellationToken cancellationToken = default);

    Task<Component3WorkflowGovernanceProjection?> GetByIdAsync(
        Guid projectionId,
        CancellationToken cancellationToken = default);

    Task<Component3WorkflowGovernanceProjection?> GetByWorkflowRunIdAndVersionAsync(
        Guid workflowRunId,
        string sourceRecordVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Component3WorkflowGovernanceProjection>> GetByCaseIdAsync(
        string caseId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);
}

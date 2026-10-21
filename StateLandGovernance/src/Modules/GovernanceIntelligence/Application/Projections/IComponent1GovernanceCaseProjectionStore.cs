namespace StateLandGovernance.GovernanceIntelligence.Application.Projections;

/// <summary>
/// Persistence contract for Component 1 case projections ingested into Component 4.
/// Supports idempotent ingestion by (CaseId, SourceRecordVersion, InputSha256).
/// </summary>
public interface IComponent1GovernanceCaseProjectionStore
{
    public const int DefaultHistoryLimit = 100;
    public const int MaxHistoryLimit = 200;

    /// <summary>
    /// Persists a Component 1 case projection idempotently.
    /// If a projection with the same (CaseId, Provenance.SourceRecordVersion, InputSha256) already exists,
    /// returns the existing persisted projection without creating a duplicate row.
    /// Throws <see cref="InvalidOperationException"/> if the same (CaseId, Provenance.SourceRecordVersion)
    /// already exists with a conflicting InputSha256.
    /// </summary>
    Task<Component1GovernanceCaseProjection> UpsertAsync(
        Component1GovernanceCaseProjection projection,
        CancellationToken cancellationToken = default);

    Task<Component1GovernanceCaseProjection?> GetByIdAsync(
        Guid projectionId,
        CancellationToken cancellationToken = default);

    Task<Component1GovernanceCaseProjection?> GetByCaseIdAndVersionAsync(
        string caseId,
        string sourceRecordVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Component1GovernanceCaseProjection>> GetByCaseIdAsync(
        string caseId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);
}

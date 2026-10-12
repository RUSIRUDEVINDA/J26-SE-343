using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed store for Component 3 workflow governance projections.
/// Enforces idempotent ingestion by <c>(WorkflowRunId, SourceRecordVersion, InputSha256)</c>
/// and preserves exact 1..N event ordering, resource strings, and naive ISO-8601 anomaly-input timestamps on round-trip.
/// </summary>
public sealed class PostgresComponent3WorkflowGovernanceProjectionStore
    : IComponent3WorkflowGovernanceProjectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresComponent3WorkflowGovernanceProjectionStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Component3WorkflowGovernanceProjection> UpsertAsync(
        Component3WorkflowGovernanceProjection projection,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(projection);

        var existing = await FindExistingOrThrowConflictAsync(projection, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var entity = MapToEntity(projection);
        await _dbContext.Component3WorkflowProjections.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return projection;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
            var concurrentWinner = await FindExistingOrThrowConflictAsync(projection, cancellationToken).ConfigureAwait(false);
            if (concurrentWinner is not null)
            {
                return concurrentWinner;
            }

            throw;
        }
    }

    public async Task<Component3WorkflowGovernanceProjection?> GetByIdAsync(
        Guid projectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (projectionId == Guid.Empty)
        {
            throw new ArgumentException("ProjectionId cannot be empty.", nameof(projectionId));
        }

        var entity = await _dbContext.Component3WorkflowProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectionId == projectionId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Component3WorkflowGovernanceProjection?> GetByWorkflowRunIdAndVersionAsync(
        Guid workflowRunId,
        string sourceRecordVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        if (string.IsNullOrWhiteSpace(sourceRecordVersion))
        {
            throw new ArgumentException("SourceRecordVersion cannot be null or whitespace.", nameof(sourceRecordVersion));
        }

        var normalizedVersion = sourceRecordVersion.Trim();
        var entity = await _dbContext.Component3WorkflowProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkflowRunId == workflowRunId && x.SourceRecordVersion == normalizedVersion,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<Component3WorkflowGovernanceProjection>> GetByCaseIdAsync(
        string caseId,
        int limit = IComponent3WorkflowGovernanceProjectionStore.DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CaseId cannot be null or whitespace.", nameof(caseId));
        }

        if (limit is < 1 or > IComponent3WorkflowGovernanceProjectionStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {IComponent3WorkflowGovernanceProjectionStore.MaxHistoryLimit}.");
        }

        var normalizedCaseId = caseId.Trim();
        var entities = await _dbContext.Component3WorkflowProjections
            .AsNoTracking()
            .Where(x => x.CaseId == normalizedCaseId)
            .OrderByDescending(x => x.IngestedAtUtc)
            .ThenByDescending(x => x.ProjectionId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    private async Task<Component3WorkflowGovernanceProjection?> FindExistingOrThrowConflictAsync(
        Component3WorkflowGovernanceProjection projection,
        CancellationToken cancellationToken)
    {
        var existingForVersion = await _dbContext.Component3WorkflowProjections
            .AsNoTracking()
            .Where(x => x.WorkflowRunId == projection.WorkflowRunId &&
                        x.SourceRecordVersion == projection.Provenance.SourceRecordVersion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var exactMatch = existingForVersion.FirstOrDefault(
            x => string.Equals(x.InputSha256, projection.InputSha256, StringComparison.Ordinal));
        if (exactMatch is not null)
        {
            return MapToDomain(exactMatch);
        }

        if (existingForVersion.Count > 0)
        {
            throw new InvalidOperationException(
                $"Conflicting Component 3 workflow projection already exists for WorkflowRunId '{projection.WorkflowRunId}' " +
                $"and SourceRecordVersion '{projection.Provenance.SourceRecordVersion}' with a different InputSha256.");
        }

        return null;
    }

    private static Component3WorkflowProjectionEntity MapToEntity(Component3WorkflowGovernanceProjection projection)
    {
        var eventSnapshots = projection.Events
            .Select(e => new StoredWorkflowEventSnapshot(
                e.EventId,
                e.EventSeq,
                e.Activity,
                e.StageCode,
                e.ActorRole,
                e.InstitutionCode,
                e.Timestamp,
                e.EventOutcome,
                e.EvidenceRef,
                e.Resource,
                e.AnomalyInputTimestamp))
            .ToList();

        return new Component3WorkflowProjectionEntity
        {
            ProjectionId = projection.ProjectionId,
            WorkflowRunId = projection.WorkflowRunId,
            CaseId = projection.CaseId,
            WorkflowType = projection.WorkflowType,
            TraceStatus = projection.TraceStatus.ToString(),
            TraceCompleteness = projection.TraceCompleteness.ToString(),
            CurrentOrTerminalStageCode = projection.CurrentOrTerminalStageCode,
            EventTimestampSemantics = projection.EventTimestampSemantics.ToString(),
            EventTimeZone = projection.EventTimeZone,
            EventCount = projection.Events.Count,
            FirstEventTimestampUtc = projection.Events[0].Timestamp.ToUniversalTime(),
            LastEventTimestampUtc = projection.Events[^1].Timestamp.ToUniversalTime(),
            FinalEventActivity = projection.Events[^1].Activity,
            EventsSnapshotJson = JsonSerializer.Serialize(eventSnapshots, JsonOptions),
            CompleteProposalRoadmapStartStatus = projection.CompleteProposalRoadmapStart.Status.ToString(),
            CompleteProposalRoadmapStartOccurredAt = projection.CompleteProposalRoadmapStart.OccurredAt?.ToUniversalTime(),
            CompleteProposalRoadmapStartSource = projection.CompleteProposalRoadmapStart.Source,
            CompleteProposalRoadmapStartSourceReference = projection.CompleteProposalRoadmapStart.SourceReference,
            ApprovalHandoverStatus = projection.ApprovalHandover.Status.ToString(),
            ApprovalHandoverOccurredAt = projection.ApprovalHandover.OccurredAt?.ToUniversalTime(),
            ApprovalHandoverSource = projection.ApprovalHandover.Source,
            ApprovalHandoverSourceReference = projection.ApprovalHandover.SourceReference,
            InputSha256 = projection.InputSha256,
            SourceSystem = projection.Provenance.SourceSystem,
            SourceRecordId = projection.Provenance.SourceRecordId,
            SourceRecordVersion = projection.Provenance.SourceRecordVersion,
            SourceObservedAtUtc = projection.Provenance.SourceObservedAtUtc,
            IsSynthetic = projection.Provenance.IsSynthetic,
            ProvenanceKind = projection.Provenance.ProvenanceKind.ToString(),
            SyntheticSeedId = projection.Provenance.SyntheticSeedId,
            SyntheticSeedVersion = projection.Provenance.SyntheticSeedVersion,
            GeneratedAtUtc = projection.Provenance.GeneratedAtUtc,
            Purpose = projection.Provenance.Purpose.ToString(),
            IngestedAtUtc = projection.IngestedAtUtc
        };
    }

    private static Component3WorkflowGovernanceProjection MapToDomain(Component3WorkflowProjectionEntity entity)
    {
        var snapshots = JsonSerializer.Deserialize<List<StoredWorkflowEventSnapshot>>(entity.EventsSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException($"Component 3 workflow projection '{entity.ProjectionId}' has null EventsSnapshotJson.");

        if (snapshots.Count != entity.EventCount)
        {
            throw new InvalidOperationException(
                $"Component 3 workflow projection '{entity.ProjectionId}' event count mismatch: scalar EventCount={entity.EventCount}, snapshot count={snapshots.Count}.");
        }

        // Preserve exact stored JSON array order so Component3WorkflowGovernanceProjection constructor
        // verifies strict 1..N contiguity without masking out-of-order storage.
        var orderedEvents = snapshots
            .Select(s =>
            {
                var restoredTimestamp = RestoreSnapshotTimestamp(s.Timestamp, s.AnomalyInputTimestamp, entity.EventTimestampSemantics);
                return new Component3WorkflowProjectionEvent(
                    eventId: s.EventId,
                    eventSeq: s.EventSeq,
                    activity: s.Activity,
                    stageCode: s.StageCode,
                    actorRole: s.ActorRole,
                    institutionCode: s.InstitutionCode,
                    timestamp: restoredTimestamp,
                    eventOutcome: s.EventOutcome,
                    evidenceRef: s.EvidenceRef,
                    resource: s.Resource ?? s.ActorRole,
                    anomalyInputTimestamp: s.AnomalyInputTimestamp);
            })
            .ToList();

        var roadmapStart = new TimingBoundaryEvidence(
            OccurredAt: entity.CompleteProposalRoadmapStartOccurredAt,
            Status: Enum.Parse<TimingBoundaryEvidenceStatus>(entity.CompleteProposalRoadmapStartStatus, ignoreCase: false),
            Source: entity.CompleteProposalRoadmapStartSource,
            SourceReference: entity.CompleteProposalRoadmapStartSourceReference,
            CaseId: entity.CaseId,
            WorkflowRunId: entity.WorkflowRunId);

        var approvalHandover = new TimingBoundaryEvidence(
            OccurredAt: entity.ApprovalHandoverOccurredAt,
            Status: Enum.Parse<TimingBoundaryEvidenceStatus>(entity.ApprovalHandoverStatus, ignoreCase: false),
            Source: entity.ApprovalHandoverSource,
            SourceReference: entity.ApprovalHandoverSourceReference,
            CaseId: entity.CaseId,
            WorkflowRunId: entity.WorkflowRunId);

        var provenance = new GovernanceInputProvenance(
            sourceSystem: entity.SourceSystem,
            sourceRecordId: entity.SourceRecordId,
            sourceRecordVersion: entity.SourceRecordVersion,
            sourceObservedAtUtc: entity.SourceObservedAtUtc,
            isSynthetic: entity.IsSynthetic,
            provenanceKind: Enum.Parse<GovernanceProvenanceKind>(entity.ProvenanceKind, ignoreCase: false),
            syntheticSeedId: entity.SyntheticSeedId,
            syntheticSeedVersion: entity.SyntheticSeedVersion,
            generatedAtUtc: entity.GeneratedAtUtc,
            purpose: Enum.Parse<GovernanceProvenancePurpose>(entity.Purpose, ignoreCase: false));

        return new Component3WorkflowGovernanceProjection(
            projectionId: entity.ProjectionId,
            workflowRunId: entity.WorkflowRunId,
            caseId: entity.CaseId,
            workflowType: entity.WorkflowType,
            traceStatus: Enum.Parse<Component3WorkflowTraceStatus>(entity.TraceStatus, ignoreCase: false),
            traceCompleteness: Enum.Parse<Component3WorkflowTraceCompleteness>(entity.TraceCompleteness, ignoreCase: false),
            currentOrTerminalStageCode: entity.CurrentOrTerminalStageCode,
            eventTimestampSemantics: Enum.Parse<Component3EventTimestampSemantics>(entity.EventTimestampSemantics, ignoreCase: false),
            eventTimeZone: entity.EventTimeZone,
            events: orderedEvents,
            completeProposalRoadmapStart: roadmapStart,
            approvalHandover: approvalHandover,
            inputSha256: entity.InputSha256,
            provenance: provenance,
            ingestedAtUtc: entity.IngestedAtUtc);
    }

    private static DateTimeOffset RestoreSnapshotTimestamp(
        DateTimeOffset storedTimestamp,
        string? anomalyInputTimestamp,
        string eventTimestampSemantics)
    {
        if (anomalyInputTimestamp is null)
        {
            return storedTimestamp;
        }

        // If PostgreSQL jsonb canonicalization normalized a +05:30 DateTimeOffset in JSON,
        // restore the exact Asia/Colombo offset when EventTimestampSemantics is Colombo-based
        // while keeping AnomalyInputTimestamp untouched as the verbatim string from JSON.
        if (storedTimestamp.Offset == TimeSpan.Zero &&
            (string.Equals(eventTimestampSemantics, nameof(Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset), StringComparison.Ordinal) ||
             string.Equals(eventTimestampSemantics, nameof(Component3EventTimestampSemantics.CalendarDateOnlyMidnightColombo), StringComparison.Ordinal)))
        {
            var colomboOffset = TimeSpan.FromMinutes(330);
            var candidate = storedTimestamp.ToOffset(colomboOffset);
            if (Component3WorkflowProjectionEvent.ValidateNaiveIso8601Timestamp(anomalyInputTimestamp, out var parsedNaive) is null &&
                candidate.DateTime == parsedNaive)
            {
                return candidate;
            }
        }

        return storedTimestamp;
    }

    private sealed record StoredWorkflowEventSnapshot(
        string EventId,
        int EventSeq,
        string Activity,
        string StageCode,
        string ActorRole,
        string InstitutionCode,
        DateTimeOffset Timestamp,
        string EventOutcome,
        string? EvidenceRef,
        string? Resource = null,
        string? AnomalyInputTimestamp = null);
}

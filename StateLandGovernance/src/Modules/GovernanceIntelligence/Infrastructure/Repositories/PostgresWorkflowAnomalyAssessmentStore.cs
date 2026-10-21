using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed store for workflow-anomaly assessments.
/// Enforces idempotent persistence by <c>(WorkflowRunId, ModelVersion, InputSha256)</c>
/// with deterministic race recovery when concurrent callers collide on the unique index.
/// </summary>
public sealed class PostgresWorkflowAnomalyAssessmentStore : IWorkflowAnomalyAssessmentStore
{
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresWorkflowAnomalyAssessmentStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<WorkflowAnomalyAssessment> UpsertAsync(
        WorkflowAnomalyAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(assessment);

        var existing = await _dbContext.WorkflowAnomalyAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkflowRunId == assessment.WorkflowRunId &&
                     x.ModelVersion == assessment.ModelVersion &&
                     x.InputSha256 == assessment.InputSha256,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return MapToDomain(existing);
        }

        var entity = MapToEntity(assessment);
        await _dbContext.WorkflowAnomalyAssessments.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return assessment;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
            var concurrentWinner = await _dbContext.WorkflowAnomalyAssessments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.WorkflowRunId == assessment.WorkflowRunId &&
                         x.ModelVersion == assessment.ModelVersion &&
                         x.InputSha256 == assessment.InputSha256,
                    cancellationToken)
                .ConfigureAwait(false);

            if (concurrentWinner is not null)
            {
                return MapToDomain(concurrentWinner);
            }

            throw;
        }
    }

    public async Task<WorkflowAnomalyAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        }

        var entity = await _dbContext.WorkflowAnomalyAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssessmentId == assessmentId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<WorkflowAnomalyAssessment?> GetByWorkflowRunModelAndHashAsync(
        Guid workflowRunId,
        string modelVersion,
        string inputSha256,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        if (string.IsNullOrWhiteSpace(modelVersion))
        {
            throw new ArgumentException("ModelVersion cannot be null or whitespace.", nameof(modelVersion));
        }

        if (string.IsNullOrWhiteSpace(inputSha256))
        {
            throw new ArgumentException("InputSha256 cannot be null or whitespace.", nameof(inputSha256));
        }

        var entity = await _dbContext.WorkflowAnomalyAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkflowRunId == workflowRunId &&
                     x.ModelVersion == modelVersion.Trim() &&
                     x.InputSha256 == inputSha256.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<WorkflowAnomalyAssessment>> GetByWorkflowRunIdAsync(
        Guid workflowRunId,
        int limit = IWorkflowAnomalyAssessmentStore.DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        if (limit is < 1 or > IWorkflowAnomalyAssessmentStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {IWorkflowAnomalyAssessmentStore.MaxHistoryLimit}.");
        }

        var entities = await _dbContext.WorkflowAnomalyAssessments
            .AsNoTracking()
            .Where(x => x.WorkflowRunId == workflowRunId)
            .OrderByDescending(x => x.AssessedAtUtc)
            .ThenByDescending(x => x.AssessmentId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<WorkflowAnomalyAssessment>> GetByCaseIdAsync(
        string caseId,
        int limit = IWorkflowAnomalyAssessmentStore.DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CaseId cannot be null or whitespace.", nameof(caseId));
        }

        if (limit is < 1 or > IWorkflowAnomalyAssessmentStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {IWorkflowAnomalyAssessmentStore.MaxHistoryLimit}.");
        }

        var normalizedCaseId = caseId.Trim();
        var entities = await _dbContext.WorkflowAnomalyAssessments
            .AsNoTracking()
            .Where(x => x.CaseId == normalizedCaseId)
            .OrderByDescending(x => x.AssessedAtUtc)
            .ThenByDescending(x => x.AssessmentId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    private static WorkflowAnomalyAssessmentEntity MapToEntity(WorkflowAnomalyAssessment assessment)
    {
        return new WorkflowAnomalyAssessmentEntity
        {
            AssessmentId = assessment.AssessmentId,
            WorkflowRunId = assessment.WorkflowRunId,
            CaseId = assessment.CaseId,
            WorkflowProjectionId = assessment.WorkflowProjectionId,
            ModelVersion = assessment.ModelVersion,
            ModelArtifactSha256 = assessment.ModelArtifactSha256,
            AnomalyScore = assessment.AnomalyScore,
            Threshold = assessment.Threshold,
            Flagged = assessment.Flagged,
            EventCount = assessment.EventCount,
            ElapsedDays = assessment.ElapsedDays,
            MaxGapHours = assessment.MaxGapHours,
            MeanGapHours = assessment.MeanGapHours,
            UniqueActivities = assessment.UniqueActivities,
            RepeatedActivityCount = assessment.RepeatedActivityCount,
            ResourceHandoffs = assessment.ResourceHandoffs,
            InstitutionSwitches = assessment.InstitutionSwitches,
            DistinctResources = assessment.DistinctResources,
            AdvisoryNote = assessment.AdvisoryNote,
            InputSha256 = assessment.InputSha256,
            SourceSystem = assessment.Provenance.SourceSystem,
            SourceRecordId = assessment.Provenance.SourceRecordId,
            SourceRecordVersion = assessment.Provenance.SourceRecordVersion,
            SourceObservedAtUtc = assessment.Provenance.SourceObservedAtUtc,
            IsSynthetic = assessment.Provenance.IsSynthetic,
            ProvenanceKind = assessment.Provenance.ProvenanceKind.ToString(),
            SyntheticSeedId = assessment.Provenance.SyntheticSeedId,
            SyntheticSeedVersion = assessment.Provenance.SyntheticSeedVersion,
            GeneratedAtUtc = assessment.Provenance.GeneratedAtUtc,
            Purpose = assessment.Provenance.Purpose.ToString(),
            AssessedAtUtc = assessment.AssessedAtUtc
        };
    }

    private static WorkflowAnomalyAssessment MapToDomain(WorkflowAnomalyAssessmentEntity entity)
    {
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

        return new WorkflowAnomalyAssessment(
            assessmentId: entity.AssessmentId,
            workflowRunId: entity.WorkflowRunId,
            caseId: entity.CaseId,
            workflowProjectionId: entity.WorkflowProjectionId,
            modelVersion: entity.ModelVersion,
            modelArtifactSha256: entity.ModelArtifactSha256,
            anomalyScore: entity.AnomalyScore,
            threshold: entity.Threshold,
            flagged: entity.Flagged,
            eventCount: entity.EventCount,
            elapsedDays: entity.ElapsedDays,
            maxGapHours: entity.MaxGapHours,
            meanGapHours: entity.MeanGapHours,
            uniqueActivities: entity.UniqueActivities,
            repeatedActivityCount: entity.RepeatedActivityCount,
            resourceHandoffs: entity.ResourceHandoffs,
            institutionSwitches: entity.InstitutionSwitches,
            distinctResources: entity.DistinctResources,
            advisoryNote: entity.AdvisoryNote,
            inputSha256: entity.InputSha256,
            provenance: provenance,
            assessedAtUtc: entity.AssessedAtUtc);
    }
}

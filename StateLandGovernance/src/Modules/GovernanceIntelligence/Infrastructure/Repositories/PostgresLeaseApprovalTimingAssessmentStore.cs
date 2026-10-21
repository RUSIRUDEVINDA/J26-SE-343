using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed store for lease-approval handover timing assessments.
/// Enforces idempotent persistence by <c>(WorkflowRunId, PolicyVersion, InputSha256)</c>
/// with deterministic race recovery when concurrent callers collide on the unique index.
/// </summary>
public sealed class PostgresLeaseApprovalTimingAssessmentStore : ILeaseApprovalTimingAssessmentStore
{
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresLeaseApprovalTimingAssessmentStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<LeaseApprovalTimingAssessment> UpsertAsync(
        LeaseApprovalTimingAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(assessment);

        var existing = await _dbContext.LeaseApprovalTimingAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkflowRunId == assessment.WorkflowRunId &&
                     x.PolicyVersion == assessment.PolicyVersion &&
                     x.InputSha256 == assessment.InputSha256,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return MapToDomain(existing);
        }

        var entity = MapToEntity(assessment);
        await _dbContext.LeaseApprovalTimingAssessments.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return assessment;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
            var concurrentWinner = await _dbContext.LeaseApprovalTimingAssessments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.WorkflowRunId == assessment.WorkflowRunId &&
                         x.PolicyVersion == assessment.PolicyVersion &&
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

    public async Task<LeaseApprovalTimingAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        }

        var entity = await _dbContext.LeaseApprovalTimingAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssessmentId == assessmentId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<LeaseApprovalTimingAssessment?> GetByWorkflowRunPolicyAndHashAsync(
        Guid workflowRunId,
        string policyVersion,
        string inputSha256,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        if (string.IsNullOrWhiteSpace(policyVersion))
        {
            throw new ArgumentException("PolicyVersion cannot be null or whitespace.", nameof(policyVersion));
        }

        if (string.IsNullOrWhiteSpace(inputSha256))
        {
            throw new ArgumentException("InputSha256 cannot be null or whitespace.", nameof(inputSha256));
        }

        var entity = await _dbContext.LeaseApprovalTimingAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WorkflowRunId == workflowRunId &&
                     x.PolicyVersion == policyVersion.Trim() &&
                     x.InputSha256 == inputSha256.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<LeaseApprovalTimingAssessment>> GetByWorkflowRunIdAsync(
        Guid workflowRunId,
        int limit = ILeaseApprovalTimingAssessmentStore.DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        if (limit is < 1 or > ILeaseApprovalTimingAssessmentStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {ILeaseApprovalTimingAssessmentStore.MaxHistoryLimit}.");
        }

        var entities = await _dbContext.LeaseApprovalTimingAssessments
            .AsNoTracking()
            .Where(x => x.WorkflowRunId == workflowRunId)
            .OrderByDescending(x => x.AssessedAtUtc)
            .ThenByDescending(x => x.AssessmentId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<LeaseApprovalTimingAssessment>> GetByCaseIdAsync(
        string caseId,
        int limit = ILeaseApprovalTimingAssessmentStore.DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CaseId cannot be null or whitespace.", nameof(caseId));
        }

        if (limit is < 1 or > ILeaseApprovalTimingAssessmentStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {ILeaseApprovalTimingAssessmentStore.MaxHistoryLimit}.");
        }

        var normalizedCaseId = caseId.Trim();
        var entities = await _dbContext.LeaseApprovalTimingAssessments
            .AsNoTracking()
            .Where(x => x.CaseId == normalizedCaseId)
            .OrderByDescending(x => x.AssessedAtUtc)
            .ThenByDescending(x => x.AssessmentId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    private static LeaseApprovalTimingAssessmentEntity MapToEntity(LeaseApprovalTimingAssessment assessment)
    {
        return new LeaseApprovalTimingAssessmentEntity
        {
            AssessmentId = assessment.AssessmentId,
            WorkflowRunId = assessment.WorkflowRunId,
            CaseId = assessment.CaseId,
            WorkflowProjectionId = assessment.WorkflowProjectionId,
            PolicyVersion = assessment.PolicyVersion,
            TimeZoneId = assessment.TimeZoneId,
            AssessmentDateColombo = assessment.AssessmentDateColombo,
            CompleteProposalRoadmapStartDate = assessment.CompleteProposalRoadmapStartDate,
            ExpectedApprovalHandoverDate = assessment.ExpectedApprovalHandoverDate,
            ApprovalHandoverDate = assessment.ApprovalHandoverDate,
            DaysElapsed = assessment.DaysElapsed,
            DaysBeyondExpected = assessment.DaysBeyondExpected,
            TimingState = assessment.TimingState.ToString(),
            EvidenceState = assessment.EvidenceState,
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

    private static LeaseApprovalTimingAssessment MapToDomain(LeaseApprovalTimingAssessmentEntity entity)
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

        return new LeaseApprovalTimingAssessment(
            assessmentId: entity.AssessmentId,
            workflowRunId: entity.WorkflowRunId,
            caseId: entity.CaseId,
            workflowProjectionId: entity.WorkflowProjectionId,
            policyVersion: entity.PolicyVersion,
            timeZoneId: entity.TimeZoneId,
            assessmentDateColombo: entity.AssessmentDateColombo,
            completeProposalRoadmapStartDate: entity.CompleteProposalRoadmapStartDate,
            expectedApprovalHandoverDate: entity.ExpectedApprovalHandoverDate,
            approvalHandoverDate: entity.ApprovalHandoverDate,
            daysElapsed: entity.DaysElapsed,
            daysBeyondExpected: entity.DaysBeyondExpected,
            timingState: Enum.Parse<LeaseApprovalTimingState>(entity.TimingState, ignoreCase: false),
            evidenceState: entity.EvidenceState,
            advisoryNote: entity.AdvisoryNote,
            inputSha256: entity.InputSha256,
            provenance: provenance,
            assessedAtUtc: entity.AssessedAtUtc);
    }
}

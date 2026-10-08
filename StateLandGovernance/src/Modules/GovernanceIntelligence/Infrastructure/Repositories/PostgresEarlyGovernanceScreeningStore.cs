using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL append/read store for early-governance assessments and durable referral intents.
/// Assessment and referral creation use one SaveChanges transaction; external handoff never
/// occurs while that database transaction is open.
/// </summary>
public sealed class PostgresEarlyGovernanceScreeningStore : IEarlyGovernanceScreeningStore
{
    public const int CurrentSchemaVersion = 1;
    private static readonly TimeSpan DeliveryLeaseDuration = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresEarlyGovernanceScreeningStore(GovernanceIntelligenceDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    // Compatibility path for historical assessment-only callers. New orchestration must use AddAssessmentAsync.
    public async Task AddAsync(
        Guid assessmentId,
        DateTimeOffset createdAtUtc,
        EarlyGovernanceScreeningResultDto result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = CreateAssessmentEntity(assessmentId, null, createdAtUtc, result);
        await _dbContext.EarlyGovernanceScreeningEvaluations.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAssessmentAsync(
        Guid assessmentId,
        Guid workflowRunId,
        DateTimeOffset createdAtUtc,
        EarlyGovernanceScreeningResultDto result,
        EarlyGovernanceReferralIntentDto? referral,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (workflowRunId == Guid.Empty)
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));

        var assessmentEntity = CreateAssessmentEntity(assessmentId, workflowRunId, createdAtUtc, result);
        await _dbContext.EarlyGovernanceScreeningEvaluations.AddAsync(assessmentEntity, cancellationToken);

        if (referral is not null)
        {
            ValidateReferral(referral, assessmentId, workflowRunId, result.CaseId);
            await _dbContext.EarlyGovernanceReferrals.AddAsync(
                new EarlyGovernanceReferralEntity
                {
                    ReferralId = referral.ReferralId,
                    CorrelationId = referral.CorrelationId,
                    AssessmentId = referral.AssessmentId,
                    CaseId = referral.CaseId,
                    WorkflowRunId = referral.WorkflowRunId,
                    ReasonCode = referral.ReasonCode,
                    Reason = referral.Reason,
                    EvidenceReferencesJson = JsonSerializer.Serialize(referral.EvidenceReferences, JsonOptions),
                    RequestedAtUtc = referral.RequestedAtUtc,
                    DeliveryState = EarlyGovernanceReferralDeliveryState.Pending.ToString(),
                    DeliveryAttemptCount = 0
                },
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredEarlyGovernanceScreeningDto?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        if (assessmentId == Guid.Empty)
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));

        var entity = await _dbContext.EarlyGovernanceScreeningEvaluations
            .Include(item => item.Referral)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.AssessmentId == assessmentId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null) return null;

        if (entity.SnapshotSchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"Unsupported snapshot schema version '{entity.SnapshotSchemaVersion}' for assessment '{assessmentId}'. Supported version is '{CurrentSchemaVersion}'.");

        EarlyGovernanceScreeningResultDto result;
        try
        {
            ValidateSnapshotStructure(assessmentId, entity.ResultSnapshotJson);
            result = JsonSerializer.Deserialize<EarlyGovernanceScreeningResultDto>(entity.ResultSnapshotJson, JsonOptions)
                ?? throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' deserialized to a null result.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' is malformed and could not be deserialized.");
        }

        if (!string.Equals(result.CaseId, entity.CaseId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Snapshot result CaseId does not match stored entity CaseId for assessment '{assessmentId}'.");
        if (!string.Equals(result.InputVersion, entity.InputVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"Snapshot result InputVersion does not match stored entity InputVersion for assessment '{assessmentId}'.");

        return new StoredEarlyGovernanceScreeningDto(
            entity.AssessmentId,
            entity.WorkflowRunId,
            entity.CreatedAtUtc,
            entity.SnapshotSchemaVersion,
            result,
            entity.Referral is null ? null : MapReferral(entity.Referral));
    }

    public async Task<StoredEarlyGovernanceReferralDto?> GetReferralByIdAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        if (referralId == Guid.Empty) throw new ArgumentException("ReferralId cannot be empty.", nameof(referralId));
        var entity = await _dbContext.EarlyGovernanceReferrals
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ReferralId == referralId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapReferral(entity);
    }

    public async Task<IReadOnlyList<StoredEarlyGovernanceReferralDto>> GetReferralHistoryByCaseIdAsync(
        string caseId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("CaseId cannot be blank.", nameof(caseId));
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 200.");

        var entities = await _dbContext.EarlyGovernanceReferrals
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.RequestedAtUtc)
            .ThenByDescending(item => item.ReferralId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(MapReferral).ToList();
    }

    public async Task<bool> TryBeginReferralDeliveryAsync(
        Guid referralId,
        int expectedAttemptCount,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (referralId == Guid.Empty) throw new ArgumentException("ReferralId cannot be empty.", nameof(referralId));
        if (expectedAttemptCount < 0) throw new ArgumentOutOfRangeException(nameof(expectedAttemptCount));
        if (attemptedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Attempt time must be UTC.", nameof(attemptedAtUtc));
        var expiredLeaseCutoff = attemptedAtUtc.Subtract(DeliveryLeaseDuration);

        int affected = await _dbContext.EarlyGovernanceReferrals
            .Where(item => item.ReferralId == referralId &&
                           item.DeliveryAttemptCount == expectedAttemptCount &&
                           (item.DeliveryState == nameof(EarlyGovernanceReferralDeliveryState.Pending) ||
                            item.DeliveryState == nameof(EarlyGovernanceReferralDeliveryState.DeliveryFailed) ||
                            (item.DeliveryState == nameof(EarlyGovernanceReferralDeliveryState.Delivering) &&
                             item.LastAttemptAtUtc <= expiredLeaseCutoff)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DeliveryState, nameof(EarlyGovernanceReferralDeliveryState.Delivering))
                .SetProperty(item => item.DeliveryAttemptCount, expectedAttemptCount + 1)
                .SetProperty(item => item.LastAttemptAtUtc, attemptedAtUtc)
                .SetProperty(item => item.LastFailureCode, (string?)null), cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    public async Task MarkReferralDeliveryFailedAsync(
        Guid referralId,
        int deliveryAttemptCount,
        string failureCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(failureCode)) throw new ArgumentException("FailureCode cannot be blank.", nameof(failureCode));
        int affected = await _dbContext.EarlyGovernanceReferrals
            .Where(item => item.ReferralId == referralId &&
                           item.DeliveryAttemptCount == deliveryAttemptCount &&
                           item.DeliveryState == nameof(EarlyGovernanceReferralDeliveryState.Delivering))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DeliveryState, nameof(EarlyGovernanceReferralDeliveryState.DeliveryFailed))
                .SetProperty(item => item.LastFailureCode, failureCode), cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1) throw new InvalidOperationException("Referral delivery state changed before failure could be recorded.");
    }

    public async Task MarkReferralAcknowledgedAsync(
        Guid referralId,
        int deliveryAttemptCount,
        DateTimeOffset acknowledgedAtUtc,
        string commissionerReviewProcessReference,
        CancellationToken cancellationToken = default)
    {
        if (acknowledgedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Acknowledgement time must be UTC.", nameof(acknowledgedAtUtc));
        if (string.IsNullOrWhiteSpace(commissionerReviewProcessReference))
            throw new ArgumentException("Commissioner review process reference cannot be blank.", nameof(commissionerReviewProcessReference));

        int affected = await _dbContext.EarlyGovernanceReferrals
            .Where(item => item.ReferralId == referralId &&
                           item.DeliveryAttemptCount == deliveryAttemptCount &&
                           item.DeliveryState == nameof(EarlyGovernanceReferralDeliveryState.Delivering))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DeliveryState, nameof(EarlyGovernanceReferralDeliveryState.Acknowledged))
                .SetProperty(item => item.AcknowledgedAtUtc, acknowledgedAtUtc)
                .SetProperty(item => item.CommissionerReviewProcessReference, commissionerReviewProcessReference)
                .SetProperty(item => item.LastFailureCode, (string?)null), cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1) throw new InvalidOperationException("Referral delivery state changed before acknowledgement could be recorded.");
    }

    private static EarlyGovernanceScreeningEvaluationEntity CreateAssessmentEntity(
        Guid assessmentId,
        Guid? workflowRunId,
        DateTimeOffset createdAtUtc,
        EarlyGovernanceScreeningResultDto result)
    {
        ValidateAssessment(assessmentId, createdAtUtc, result);
        return new EarlyGovernanceScreeningEvaluationEntity
        {
            AssessmentId = assessmentId,
            WorkflowRunId = workflowRunId,
            CaseId = result.CaseId,
            InputVersion = result.InputVersion,
            CreatedAtUtc = createdAtUtc,
            SnapshotSchemaVersion = CurrentSchemaVersion,
            ResultSnapshotJson = JsonSerializer.Serialize(result, JsonOptions)
        };
    }

    private static void ValidateAssessment(Guid assessmentId, DateTimeOffset createdAtUtc, EarlyGovernanceScreeningResultDto result)
    {
        if (assessmentId == Guid.Empty) throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("CreatedAtUtc must have a zero UTC offset.", nameof(createdAtUtc));
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(result.CaseId)) throw new ArgumentException("Result CaseId cannot be blank.", nameof(result));
        if (string.IsNullOrWhiteSpace(result.InputVersion)) throw new ArgumentException("Result InputVersion cannot be blank.", nameof(result));
        if (result.IndicatorResults is null || result.IndicatorResults.Any(item => item is null))
            throw new ArgumentException("Result IndicatorResults must contain only non-null entries.", nameof(result));
    }

    private static void ValidateReferral(EarlyGovernanceReferralIntentDto referral, Guid assessmentId, Guid workflowRunId, string caseId)
    {
        if (referral.ReferralId == Guid.Empty || referral.CorrelationId == Guid.Empty)
            throw new ArgumentException("Referral and correlation identities cannot be empty.", nameof(referral));
        if (referral.AssessmentId != assessmentId || referral.WorkflowRunId != workflowRunId ||
            !string.Equals(referral.CaseId, caseId, StringComparison.Ordinal))
            throw new ArgumentException("Referral case, workflow run, and assessment identities must match the screening assessment.", nameof(referral));
        if (string.IsNullOrWhiteSpace(referral.ReasonCode) || string.IsNullOrWhiteSpace(referral.Reason))
            throw new ArgumentException("Referral reason code and reason are required.", nameof(referral));
        if (referral.RequestedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Referral request time must be UTC.", nameof(referral));
        if (referral.EvidenceReferences is null || referral.EvidenceReferences.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Referral evidence references cannot be null or blank.", nameof(referral));
    }

    private static StoredEarlyGovernanceReferralDto MapReferral(EarlyGovernanceReferralEntity entity)
    {
        if (!Enum.TryParse<EarlyGovernanceReferralDeliveryState>(entity.DeliveryState, out var state))
            throw new InvalidOperationException($"Referral '{entity.ReferralId}' has an unsupported delivery state.");

        IReadOnlyList<string> evidence;
        try
        {
            evidence = JsonSerializer.Deserialize<List<string>>(entity.EvidenceReferencesJson, JsonOptions)
                ?? throw new InvalidOperationException($"Referral '{entity.ReferralId}' evidence deserialized to null.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Referral '{entity.ReferralId}' has malformed evidence references.");
        }

        return new StoredEarlyGovernanceReferralDto(
            entity.ReferralId, entity.CorrelationId, entity.AssessmentId, entity.CaseId,
            entity.WorkflowRunId, entity.ReasonCode, entity.Reason, evidence,
            entity.RequestedAtUtc, state, entity.DeliveryAttemptCount, entity.LastAttemptAtUtc,
            entity.AcknowledgedAtUtc, entity.CommissionerReviewProcessReference, entity.LastFailureCode);
    }

    private static void ValidateSnapshotStructure(Guid assessmentId, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' is invalid: root must be a JSON object.");
        RequireNonBlankStringProperty(root, "caseId", assessmentId);
        RequireNonBlankStringProperty(root, "inputVersion", assessmentId);
        RequireNonBlankStringProperty(root, "overallStatus", assessmentId);
        RequireBooleanProperty(root, "hasIncompleteEvidence", assessmentId);
        if (!root.TryGetProperty("indicatorResults", out var indicators) || indicators.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' is invalid: missing or invalid 'indicatorResults' array.");
        int index = 0;
        foreach (var indicator in indicators.EnumerateArray())
        {
            if (indicator.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' is invalid: indicator at index {index} must be a JSON object.");
            RequireNonBlankStringProperty(indicator, "indicatorType", assessmentId, index);
            RequireNonBlankStringProperty(indicator, "evidenceState", assessmentId, index);
            RequireBooleanProperty(indicator, "requiresReview", assessmentId, index);
            RequireBooleanProperty(indicator, "needsEvidence", assessmentId, index);
            RequireNonBlankStringProperty(indicator, "reasonCode", assessmentId, index);
            RequireNonBlankStringProperty(indicator, "message", assessmentId, index);
            index++;
        }
    }

    private static void RequireNonBlankStringProperty(JsonElement element, string propertyName, Guid assessmentId, int? index = null)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' is invalid: missing or blank '{propertyName}' in {(index.HasValue ? $"indicator at index {index}" : "root")}.");
    }

    private static void RequireBooleanProperty(JsonElement element, string propertyName, Guid assessmentId, int? index = null)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"Snapshot for assessment '{assessmentId}' is invalid: missing or invalid boolean '{propertyName}' in {(index.HasValue ? $"indicator at index {index}" : "root")}.");
    }
}

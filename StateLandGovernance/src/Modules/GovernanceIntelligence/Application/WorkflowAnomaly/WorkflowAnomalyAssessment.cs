using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;

namespace StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

/// <summary>
/// Immutable persisted workflow-anomaly assessment record.
/// <para>
/// Governance &amp; Statistical Boundary: <see cref="AnomalyScore"/> is an Isolation Forest decision-function score
/// and is explicitly NOT a calibrated probability, confidence value, risk percentage, or likelihood.
/// Decisions are advisory only and require human review.
/// </para>
/// </summary>
public sealed record WorkflowAnomalyAssessment
{
    public const string FrozenModelVersionV1 = "workflow_anomaly_model_v1";
    public const string FrozenModelArtifactSha256V1 = "59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46";
    public const double FrozenThresholdV1 = 0.5743688923115832;

    /// <summary>
    /// Terminal activity required by the frozen <c>workflow_anomaly_model_v1</c> synthetic completed-trace contract.
    /// Denotes model-scoring eligibility only; does NOT represent legal lease completion, approval granted,
    /// approval handover, Component 3 business completion, or satisfaction of the two-calendar-month timing policy.
    /// </summary>
    public const string AnomalyModelV1TerminalActivity = "DS Final Notification";

    public const int MaxCaseIdLength = 128;
    public const int MaxModelVersionLength = 64;
    public const int MaxAdvisoryNoteLength = 1024;
    private static readonly Regex Sha256HexRegex = new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions CanonicalHashJsonOptions = new()
    {
        WriteIndented = false
    };

    public WorkflowAnomalyAssessment(
        Guid assessmentId,
        Guid workflowRunId,
        string caseId,
        Guid? workflowProjectionId,
        string modelVersion,
        string? modelArtifactSha256,
        double anomalyScore,
        double threshold,
        bool flagged,
        int eventCount,
        double elapsedDays,
        double maxGapHours,
        double meanGapHours,
        int uniqueActivities,
        int repeatedActivityCount,
        int resourceHandoffs,
        int institutionSwitches,
        int distinctResources,
        string advisoryNote,
        string inputSha256,
        GovernanceInputProvenance provenance,
        DateTimeOffset assessedAtUtc)
    {
        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        }

        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        if (workflowProjectionId.HasValue && workflowProjectionId.Value == Guid.Empty)
        {
            throw new ArgumentException("WorkflowProjectionId cannot be Guid.Empty when provided.", nameof(workflowProjectionId));
        }

        CaseId = RequireNormalized(caseId, nameof(caseId), MaxCaseIdLength);
        ModelVersion = RequireNormalized(modelVersion, nameof(modelVersion), MaxModelVersionLength);
        AdvisoryNote = RequireNormalized(advisoryNote, nameof(advisoryNote), MaxAdvisoryNoteLength);

        if (modelArtifactSha256 is not null)
        {
            var trimmedModelSha = modelArtifactSha256.Trim();
            if (!Sha256HexRegex.IsMatch(trimmedModelSha))
            {
                throw new ArgumentException(
                    "ModelArtifactSha256 must be a 64-character lowercase hexadecimal SHA-256 digest when provided.",
                    nameof(modelArtifactSha256));
            }

            ModelArtifactSha256 = trimmedModelSha;
        }

        if (!double.IsFinite(anomalyScore))
        {
            throw new ArgumentOutOfRangeException(nameof(anomalyScore), "AnomalyScore must be a finite number.");
        }

        if (!double.IsFinite(threshold))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be a finite number.");
        }

        bool expectedFlagged = anomalyScore > threshold;
        if (flagged != expectedFlagged)
        {
            throw new ArgumentException(
                $"Flagged ({flagged}) must equal the strict comparison AnomalyScore > Threshold ({anomalyScore} > {threshold} = {expectedFlagged}).",
                nameof(flagged));
        }

        if (!double.IsFinite(elapsedDays) || elapsedDays < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedDays), "ElapsedDays must be a finite non-negative number.");
        }

        if (!double.IsFinite(maxGapHours) || maxGapHours < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxGapHours), "MaxGapHours must be a finite non-negative number.");
        }

        if (!double.IsFinite(meanGapHours) || meanGapHours < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(meanGapHours), "MeanGapHours must be a finite non-negative number.");
        }

        if (eventCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventCount), "EventCount must be strictly greater than zero.");
        }

        if (uniqueActivities <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(uniqueActivities), "UniqueActivities must be strictly greater than zero.");
        }

        if (repeatedActivityCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatedActivityCount), "RepeatedActivityCount cannot be negative.");
        }

        if (resourceHandoffs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resourceHandoffs), "ResourceHandoffs cannot be negative.");
        }

        if (institutionSwitches < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(institutionSwitches), "InstitutionSwitches cannot be negative.");
        }

        if (distinctResources <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distinctResources), "DistinctResources must be strictly greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(inputSha256) || !Sha256HexRegex.IsMatch(inputSha256))
        {
            throw new ArgumentException("InputSha256 must be a 64-character lowercase hexadecimal SHA-256 digest.", nameof(inputSha256));
        }

        ArgumentNullException.ThrowIfNull(provenance);

        if (assessedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("AssessedAtUtc must have a zero UTC offset.", nameof(assessedAtUtc));
        }

        AssessmentId = assessmentId;
        WorkflowRunId = workflowRunId;
        WorkflowProjectionId = workflowProjectionId;
        AnomalyScore = anomalyScore;
        Threshold = threshold;
        Flagged = flagged;
        EventCount = eventCount;
        ElapsedDays = elapsedDays;
        MaxGapHours = maxGapHours;
        MeanGapHours = meanGapHours;
        UniqueActivities = uniqueActivities;
        RepeatedActivityCount = repeatedActivityCount;
        ResourceHandoffs = resourceHandoffs;
        InstitutionSwitches = institutionSwitches;
        DistinctResources = distinctResources;
        InputSha256 = inputSha256;
        Provenance = provenance;
        AssessedAtUtc = assessedAtUtc;
    }

    public Guid AssessmentId { get; }
    public Guid WorkflowRunId { get; }
    public string CaseId { get; }
    public Guid? WorkflowProjectionId { get; }
    public string ModelVersion { get; }
    public string? ModelArtifactSha256 { get; }

    /// <summary>
    /// Isolation Forest anomaly score. Explicitly NOT a calibrated probability, confidence value, risk percentage, or likelihood.
    /// </summary>
    public double AnomalyScore { get; }

    public double Threshold { get; }
    public bool Flagged { get; }
    public int EventCount { get; }
    public double ElapsedDays { get; }
    public double MaxGapHours { get; }
    public double MeanGapHours { get; }
    public int UniqueActivities { get; }
    public int RepeatedActivityCount { get; }
    public int ResourceHandoffs { get; }
    public int InstitutionSwitches { get; }
    public int DistinctResources { get; }
    public string AdvisoryNote { get; }
    public string InputSha256 { get; }
    public GovernanceInputProvenance Provenance { get; }
    public DateTimeOffset AssessedAtUtc { get; }

    public WorkflowFeatureValues ToFeatureValues() =>
        new(
            EventCount: EventCount,
            ElapsedDays: ElapsedDays,
            MaxGapHours: MaxGapHours,
            MeanGapHours: MeanGapHours,
            UniqueActivities: UniqueActivities,
            RepeatedActivityCount: RepeatedActivityCount,
            ResourceHandoffs: ResourceHandoffs,
            InstitutionSwitches: InstitutionSwitches,
            DistinctResources: DistinctResources);

    /// <summary>
    /// Computes the deterministic canonical SHA-256 over the exact workflow anomaly scoring input
    /// (<paramref name="caseId"/> and ordered <paramref name="events"/>) in the exact order and verbatim string
    /// representation submitted to <c>IWorkflowAnomalyClient</c>, without reordering or normalizing timestamps.
    /// </summary>
    public static string ComputeCanonicalInputSha256(
        Guid workflowRunId,
        string caseId,
        IReadOnlyList<WorkflowAnomalyEvent> events,
        GovernanceInputProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(provenance);

        var canonicalEnvelope = new CanonicalScoringInputEnvelope(
            Schema: "workflow_anomaly_input_v1",
            WorkflowRunId: workflowRunId.ToString("D"),
            CaseId: caseId,
            Events: events.Select(ev => new CanonicalScoringEvent(
                ev.EventSeq,
                ev.Activity,
                ev.Institution,
                ev.Resource,
                ev.Timestamp)).ToArray());

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(canonicalEnvelope, CanonicalHashJsonOptions);
        var hashBytes = SHA256.HashData(jsonBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private sealed record CanonicalScoringInputEnvelope(
        string Schema,
        string WorkflowRunId,
        string CaseId,
        CanonicalScoringEvent[] Events);

    private sealed record CanonicalScoringEvent(
        int EventSeq,
        string Activity,
        string Institution,
        string Resource,
        string Timestamp);

    private static string RequireNormalized(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or whitespace.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);
        }

        return trimmed;
    }
}

/// <summary>
/// Application-level orchestration result for workflow anomaly assessment and persistence.
/// </summary>
public sealed record WorkflowAnomalyAssessmentResult
{
    private WorkflowAnomalyAssessmentResult(
        WorkflowAnomalyAssessment? assessment,
        WorkflowAnomalyError? error)
    {
        Assessment = assessment;
        Error = error;
    }

    public bool IsSuccess => Assessment is not null;
    public WorkflowAnomalyAssessment? Assessment { get; }
    public WorkflowAnomalyError? Error { get; }

    public static WorkflowAnomalyAssessmentResult Success(WorkflowAnomalyAssessment assessment) =>
        new(assessment ?? throw new ArgumentNullException(nameof(assessment)), null);

    public static WorkflowAnomalyAssessmentResult Failure(
        WorkflowAnomalyErrorCode code,
        string message) =>
        new(null, new WorkflowAnomalyError(code, message ?? throw new ArgumentNullException(nameof(message))));

    public static WorkflowAnomalyAssessmentResult Failure(WorkflowAnomalyError error) =>
        new(null, error ?? throw new ArgumentNullException(nameof(error)));
}

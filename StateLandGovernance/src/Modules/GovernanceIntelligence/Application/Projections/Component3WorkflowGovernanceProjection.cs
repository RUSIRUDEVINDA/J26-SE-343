using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Application.Projections;

/// <summary>
/// Closed vocabulary for the lifecycle status of a Component 3 workflow trace.
/// </summary>
public enum Component3WorkflowTraceStatus
{
    Completed = 1,
    Ongoing = 2,
    Stopped = 3,
    Cancelled = 4,
    Suspended = 5
}

/// <summary>
/// Closed vocabulary for whether a Component 3 workflow trace satisfies the frozen
/// <c>workflow_anomaly_model_v1</c> completed-trace eligibility contract.
/// <para>
/// Governance Boundary: <see cref="CompletedTraceEligibleForAnomalyScoring"/> denotes ONLY that the trace
/// satisfies the structural input eligibility rules of the current Isolation Forest model
/// (complete trace ending in <c>"DS Final Notification"</c>). It does NOT mean legal lease-process completion,
/// approval granted, approval handed to the applicant, Component 3 business completion, or satisfaction
/// of the two-calendar-month timing expectation.
/// </para>
/// </summary>
public enum Component3WorkflowTraceCompleteness
{
    CompletedTraceEligibleForAnomalyScoring = 1,
    PartialTraceIneligibleForAnomalyScoring = 2,
    InvalidOrIncompleteTrace = 3
}

/// <summary>
/// Closed vocabulary for how Component 3 event timestamps were observed or normalized.
/// </summary>
public enum Component3EventTimestampSemantics
{
    ExactUtcTimestamp = 1,
    LocalBusinessDayWithColomboOffset = 2,
    CalendarDateOnlyMidnightColombo = 3
}

/// <summary>
/// Immutable single event within a Component 3 workflow governance projection.
/// Retains both the normalized <see cref="Timestamp"/> for projection queries/chronology and the exact
/// <see cref="Resource"/> and naive ISO-8601 <see cref="AnomalyInputTimestamp"/> required for lossless
/// workflow-anomaly model scoring.
/// </summary>
public sealed record Component3WorkflowProjectionEvent
{
    public const int MaxIdentifierLength = 128;
    public const int MaxActivityLength = 128;
    public const int MaxCodeLength = 64;
    public const int MaxRoleLength = 64;
    public const int MaxResourceLength = 128;
    public const int MaxOutcomeLength = 64;
    public const int MaxEvidenceRefLength = 256;
    public const int MaxNaiveTimestampLength = 64;

    public Component3WorkflowProjectionEvent(
        string eventId,
        int eventSeq,
        string activity,
        string stageCode,
        string actorRole,
        string institutionCode,
        DateTimeOffset timestamp,
        string eventOutcome,
        string? evidenceRef = null,
        string? resource = null,
        string? anomalyInputTimestamp = null)
    {
        EventId = RequireNormalized(eventId, nameof(eventId), MaxIdentifierLength);
        if (eventSeq < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(eventSeq), "EventSeq must be >= 1.");
        }

        if (timestamp == default)
        {
            throw new ArgumentException("Timestamp must be an explicit offset-aware instant.", nameof(timestamp));
        }

        EventSeq = eventSeq;
        Activity = RequireNormalized(activity, nameof(activity), MaxActivityLength);
        StageCode = RequireNormalized(stageCode, nameof(stageCode), MaxCodeLength);
        ActorRole = RequireNormalized(actorRole, nameof(actorRole), MaxRoleLength);
        InstitutionCode = RequireNormalized(institutionCode, nameof(institutionCode), MaxCodeLength);
        Timestamp = timestamp;
        EventOutcome = RequireNormalized(eventOutcome, nameof(eventOutcome), MaxOutcomeLength);

        Resource = resource is null
            ? ActorRole
            : RequireNormalized(resource, nameof(resource), MaxResourceLength);

        if (anomalyInputTimestamp is not null)
        {
            var timestampError = ValidateNaiveIso8601Timestamp(anomalyInputTimestamp, out var parsedNaive);
            if (timestampError is not null)
            {
                throw new ArgumentException(timestampError, nameof(anomalyInputTimestamp));
            }

            if (timestamp.DateTime != parsedNaive)
            {
                throw new ArgumentException(
                    $"AnomalyInputTimestamp ('{anomalyInputTimestamp}') wall-clock date/time must match Timestamp.DateTime ('{timestamp.DateTime:O}') without silent UTC conversion.",
                    nameof(anomalyInputTimestamp));
            }

            AnomalyInputTimestamp = anomalyInputTimestamp;
        }

        if (evidenceRef is not null)
        {
            var trimmedRef = evidenceRef.Trim();
            if (trimmedRef.Length == 0)
            {
                throw new ArgumentException("EvidenceRef cannot be whitespace when provided.", nameof(evidenceRef));
            }

            if (trimmedRef.Length > MaxEvidenceRefLength)
            {
                throw new ArgumentException($"EvidenceRef cannot exceed {MaxEvidenceRefLength} characters.", nameof(evidenceRef));
            }

            EvidenceRef = trimmedRef;
        }
    }

    public string EventId { get; }
    public int EventSeq { get; }
    public string Activity { get; }
    public string StageCode { get; }
    public string ActorRole { get; }
    public string InstitutionCode { get; }
    public DateTimeOffset Timestamp { get; }
    public string EventOutcome { get; }
    public string? EvidenceRef { get; }

    /// <summary>
    /// Exact resource identifier/label preserved for workflow-anomaly feature extraction.
    /// Defaults to <see cref="ActorRole"/> when not separately supplied.
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Exact naive ISO-8601 timestamp string (without 'Z' or timezone offset) preserved verbatim for
    /// workflow-anomaly model scoring. Never reconstructed by stripping offsets from normalized UTC timestamps.
    /// </summary>
    public string? AnomalyInputTimestamp { get; }

    /// <summary>
    /// Validates that a timestamp string is a naive ISO-8601 date-time with no 'Z' suffix, no timezone offset,
    /// and no leading or trailing whitespace.
    /// </summary>
    public static string? ValidateNaiveIso8601Timestamp(string? timestamp, out DateTime parsedNaive)
    {
        parsedNaive = default;

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return "Naive timestamp must be a non-blank string.";
        }

        if (!string.Equals(timestamp, timestamp.Trim(), StringComparison.Ordinal))
        {
            return "Naive timestamp must not contain leading or trailing whitespace.";
        }

        if (timestamp.Length > MaxNaiveTimestampLength)
        {
            return $"Naive timestamp cannot exceed {MaxNaiveTimestampLength} characters.";
        }

        if (timestamp.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return "Naive timestamp must not contain a UTC 'Z' indicator.";
        }

        if (!timestamp.Contains('T', StringComparison.Ordinal))
        {
            return "Naive timestamp must be an ISO-8601 date-time string containing 'T'.";
        }

        if (timestamp.Length > 10 && (timestamp[10..].Contains('+') || timestamp[10..].Contains('-')))
        {
            return "Naive timestamp must not contain a timezone offset.";
        }

        if (!DateTime.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedNaive))
        {
            return "Naive timestamp is not a valid ISO-8601 date-time string.";
        }

        return null;
    }

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
/// Immutable Component 4 read-model projection of a Component 3 workflow trace and its lease-approval timing boundaries.
/// Preserves strict module boundaries without referencing Component 3 domain or application types.
/// </summary>
public sealed record Component3WorkflowGovernanceProjection
{
    public const string RequiredColomboTimeZone = "Asia/Colombo";

    /// <summary>
    /// Terminal activity required by the frozen <c>workflow_anomaly_model_v1</c> synthetic completed-trace contract.
    /// Denotes model-scoring eligibility only; does NOT represent legal lease completion, approval granted,
    /// approval handover, Component 3 business completion, or satisfaction of the two-calendar-month timing policy.
    /// </summary>
    public const string AnomalyModelV1TerminalActivity = "DS Final Notification";

    public const string RequiredAnomalyTerminalActivity = AnomalyModelV1TerminalActivity;
    public const int MaxIdentifierLength = 128;
    public const int MaxWorkflowTypeLength = 64;
    public const int MaxStageCodeLength = 64;
    public const int MaxTimeZoneLength = 64;
    private static readonly Regex Sha256HexRegex = new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Component3WorkflowGovernanceProjection(
        Guid projectionId,
        Guid workflowRunId,
        string caseId,
        string workflowType,
        Component3WorkflowTraceStatus traceStatus,
        Component3WorkflowTraceCompleteness traceCompleteness,
        string currentOrTerminalStageCode,
        Component3EventTimestampSemantics eventTimestampSemantics,
        string eventTimeZone,
        IReadOnlyList<Component3WorkflowProjectionEvent> events,
        TimingBoundaryEvidence completeProposalRoadmapStart,
        TimingBoundaryEvidence approvalHandover,
        string inputSha256,
        GovernanceInputProvenance provenance,
        DateTimeOffset ingestedAtUtc)
    {
        if (projectionId == Guid.Empty)
        {
            throw new ArgumentException("ProjectionId cannot be empty.", nameof(projectionId));
        }

        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }

        CaseId = RequireNormalized(caseId, nameof(caseId), MaxIdentifierLength);
        WorkflowType = RequireNormalized(workflowType, nameof(workflowType), MaxWorkflowTypeLength);
        CurrentOrTerminalStageCode = RequireNormalized(
            currentOrTerminalStageCode,
            nameof(currentOrTerminalStageCode),
            MaxStageCodeLength);

        if (!Enum.IsDefined(traceStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(traceStatus), "TraceStatus must be a defined value.");
        }

        if (!Enum.IsDefined(traceCompleteness))
        {
            throw new ArgumentOutOfRangeException(nameof(traceCompleteness), "TraceCompleteness must be a defined value.");
        }

        if (!Enum.IsDefined(eventTimestampSemantics))
        {
            throw new ArgumentOutOfRangeException(nameof(eventTimestampSemantics), "EventTimestampSemantics must be a defined value.");
        }

        if (string.IsNullOrWhiteSpace(eventTimeZone) ||
            !string.Equals(eventTimeZone.Trim(), RequiredColomboTimeZone, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"EventTimeZone must equal '{RequiredColomboTimeZone}'.",
                nameof(eventTimeZone));
        }

        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            throw new ArgumentException("Events must contain at least one workflow event.", nameof(events));
        }

        var validatedEvents = ValidateOrderedEvents(events);

        if (traceStatus == Component3WorkflowTraceStatus.Ongoing &&
            traceCompleteness == Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring)
        {
            throw new ArgumentException(
                "When TraceStatus is Ongoing, TraceCompleteness cannot be CompletedTraceEligibleForAnomalyScoring.",
                nameof(traceCompleteness));
        }

        if (traceCompleteness == Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring)
        {
            var finalActivity = validatedEvents[^1].Activity;
            if (!string.Equals(finalActivity, AnomalyModelV1TerminalActivity, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"When TraceCompleteness is CompletedTraceEligibleForAnomalyScoring (model-specific eligibility for workflow_anomaly_model_v1), the final event activity must be '{AnomalyModelV1TerminalActivity}', but was '{finalActivity}'.",
                    nameof(events));
            }
        }

        ValidateTimingBoundaryEvidence(completeProposalRoadmapStart, CaseId, workflowRunId, nameof(completeProposalRoadmapStart));
        ValidateTimingBoundaryEvidence(approvalHandover, CaseId, workflowRunId, nameof(approvalHandover));

        if (traceStatus == Component3WorkflowTraceStatus.Ongoing &&
            approvalHandover.Status == TimingBoundaryEvidenceStatus.Confirmed)
        {
            throw new ArgumentException(
                "An ongoing workflow trace cannot contain a confirmed approval-handover boundary.",
                nameof(approvalHandover));
        }

        if (string.IsNullOrWhiteSpace(inputSha256) || !Sha256HexRegex.IsMatch(inputSha256))
        {
            throw new ArgumentException("InputSha256 must be a 64-character lowercase hexadecimal SHA-256 digest.", nameof(inputSha256));
        }

        ArgumentNullException.ThrowIfNull(provenance);

        if (ingestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("IngestedAtUtc must have a zero UTC offset.", nameof(ingestedAtUtc));
        }

        ProjectionId = projectionId;
        WorkflowRunId = workflowRunId;
        TraceStatus = traceStatus;
        TraceCompleteness = traceCompleteness;
        EventTimestampSemantics = eventTimestampSemantics;
        EventTimeZone = RequiredColomboTimeZone;
        Events = validatedEvents;
        CompleteProposalRoadmapStart = completeProposalRoadmapStart;
        ApprovalHandover = approvalHandover;
        InputSha256 = inputSha256;
        Provenance = provenance;
        IngestedAtUtc = ingestedAtUtc;
    }

    public Guid ProjectionId { get; }
    public Guid WorkflowRunId { get; }
    public string CaseId { get; }
    public string WorkflowType { get; }
    public Component3WorkflowTraceStatus TraceStatus { get; }
    public Component3WorkflowTraceCompleteness TraceCompleteness { get; }
    public string CurrentOrTerminalStageCode { get; }
    public Component3EventTimestampSemantics EventTimestampSemantics { get; }
    public string EventTimeZone { get; }
    public IReadOnlyList<Component3WorkflowProjectionEvent> Events { get; }
    public TimingBoundaryEvidence CompleteProposalRoadmapStart { get; }
    public TimingBoundaryEvidence ApprovalHandover { get; }
    public string InputSha256 { get; }
    public GovernanceInputProvenance Provenance { get; }
    public DateTimeOffset IngestedAtUtc { get; }

    /// <summary>
    /// Extracts the exact ordered <see cref="WorkflowAnomalyEvent"/> sequence for anomaly model scoring.
    /// Fails closed if the projection is not eligible for anomaly scoring or if any event does not retain
    /// its exact naive ISO-8601 <see cref="Component3WorkflowProjectionEvent.AnomalyInputTimestamp"/>.
    /// Never reconstructs naive timestamps by stripping offsets from normalized <see cref="DateTimeOffset"/> values.
    /// </summary>
    public bool TryExtractAnomalyScoringEvents(
        out IReadOnlyList<WorkflowAnomalyEvent> anomalyEvents,
        out string? failureReason)
    {
        anomalyEvents = Array.Empty<WorkflowAnomalyEvent>();
        failureReason = null;

        if (TraceStatus == Component3WorkflowTraceStatus.Ongoing ||
            TraceCompleteness != Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring)
        {
            failureReason =
                $"Workflow projection '{ProjectionId}' (TraceStatus={TraceStatus}, TraceCompleteness={TraceCompleteness}) is not eligible for workflow-anomaly model scoring.";
            return false;
        }

        if (Events.Count == 0 ||
            !string.Equals(Events[^1].Activity, AnomalyModelV1TerminalActivity, StringComparison.Ordinal))
        {
            failureReason =
                $"Workflow projection '{ProjectionId}' does not end with the workflow_anomaly_model_v1 terminal activity '{AnomalyModelV1TerminalActivity}'.";
            return false;
        }

        var extracted = new List<WorkflowAnomalyEvent>(Events.Count);
        DateTime? previousNaive = null;

        for (int i = 0; i < Events.Count; i++)
        {
            var ev = Events[i];
            if (string.IsNullOrWhiteSpace(ev.AnomalyInputTimestamp))
            {
                failureReason =
                    $"Workflow projection '{ProjectionId}' event at sequence {ev.EventSeq} does not retain an explicit naive ISO-8601 AnomalyInputTimestamp; refusing to reconstruct from normalized DateTimeOffset.";
                return false;
            }

            var timestampError = Component3WorkflowProjectionEvent.ValidateNaiveIso8601Timestamp(
                ev.AnomalyInputTimestamp,
                out var parsedNaive);
            if (timestampError is not null)
            {
                failureReason =
                    $"Workflow projection '{ProjectionId}' event at sequence {ev.EventSeq} has an invalid AnomalyInputTimestamp: {timestampError}";
                return false;
            }

            if (previousNaive.HasValue && parsedNaive < previousNaive.Value)
            {
                failureReason =
                    $"Workflow projection '{ProjectionId}' event at sequence {ev.EventSeq} has a non-monotonic AnomalyInputTimestamp '{ev.AnomalyInputTimestamp}'.";
                return false;
            }

            previousNaive = parsedNaive;
            extracted.Add(new WorkflowAnomalyEvent(
                EventSeq: ev.EventSeq,
                Activity: ev.Activity,
                Institution: ev.InstitutionCode,
                Resource: ev.Resource,
                Timestamp: ev.AnomalyInputTimestamp));
        }

        anomalyEvents = new ReadOnlyCollection<WorkflowAnomalyEvent>(extracted);
        return true;
    }

    /// <summary>
    /// Computes the deterministic canonical SHA-256 digest over all outcome-affecting Component 3 workflow projection fields,
    /// ordered events (including preserved anomaly-input resource and naive timestamp when present), timing boundaries, and provenance metadata.
    /// </summary>
    public static string ComputeCanonicalSha256(
        Guid workflowRunId,
        string caseId,
        string workflowType,
        Component3WorkflowTraceStatus traceStatus,
        Component3WorkflowTraceCompleteness traceCompleteness,
        string currentOrTerminalStageCode,
        Component3EventTimestampSemantics eventTimestampSemantics,
        string eventTimeZone,
        IReadOnlyList<Component3WorkflowProjectionEvent> events,
        TimingBoundaryEvidence completeProposalRoadmapStart,
        TimingBoundaryEvidence approvalHandover,
        GovernanceInputProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(completeProposalRoadmapStart);
        ArgumentNullException.ThrowIfNull(approvalHandover);
        ArgumentNullException.ThrowIfNull(provenance);

        var validatedEvents = ValidateOrderedEvents(events);
        var builder = new StringBuilder();
        builder.Append("c3_projection_v1|");
        builder.Append(workflowRunId.ToString("D", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(caseId.Trim()).Append('|');
        builder.Append(workflowType.Trim()).Append('|');
        builder.Append(traceStatus).Append('|');
        builder.Append(traceCompleteness).Append('|');
        builder.Append(currentOrTerminalStageCode.Trim()).Append('|');
        builder.Append(eventTimestampSemantics).Append('|');
        builder.Append(eventTimeZone.Trim()).Append('|');

        foreach (var ev in validatedEvents)
        {
            builder.Append(ev.EventSeq.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(ev.EventId).Append(':')
                .Append(ev.Activity).Append(':')
                .Append(ev.StageCode).Append(':')
                .Append(ev.ActorRole).Append(':')
                .Append(ev.Resource).Append(':')
                .Append(ev.InstitutionCode).Append(':')
                .Append(ev.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append(':')
                .Append(ev.AnomalyInputTimestamp ?? string.Empty).Append(':')
                .Append(ev.EventOutcome).Append(':')
                .Append(ev.EvidenceRef ?? string.Empty).Append(';');
        }

        builder.Append('|');
        AppendTimingBoundary(builder, completeProposalRoadmapStart);
        builder.Append('|');
        AppendTimingBoundary(builder, approvalHandover);
        builder.Append('|');
        builder.Append(provenance.SourceSystem).Append('|');
        builder.Append(provenance.SourceRecordId).Append('|');
        builder.Append(provenance.SourceRecordVersion).Append('|');
        builder.Append(provenance.IsSynthetic ? "1" : "0").Append('|');
        builder.Append(provenance.ProvenanceKind).Append('|');
        builder.Append(provenance.SyntheticSeedId ?? string.Empty).Append('|');
        builder.Append(provenance.SyntheticSeedVersion ?? string.Empty).Append('|');
        builder.Append(provenance.Purpose);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendTimingBoundary(StringBuilder builder, TimingBoundaryEvidence boundary)
    {
        builder.Append(boundary.Status).Append(':')
            .Append(boundary.OccurredAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
            .Append(boundary.Source.Trim()).Append(':')
            .Append(boundary.SourceReference.Trim()).Append(':')
            .Append(boundary.CaseId.Trim()).Append(':')
            .Append(boundary.WorkflowRunId.ToString("D", CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<Component3WorkflowProjectionEvent> ValidateOrderedEvents(
        IReadOnlyList<Component3WorkflowProjectionEvent> events)
    {
        var copy = new List<Component3WorkflowProjectionEvent>(events.Count);
        var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
        DateTimeOffset? previousInstantUtc = null;
        DateTime? previousNaive = null;

        for (var i = 0; i < events.Count; i++)
        {
            var current = events[i] ?? throw new ArgumentException($"Events[{i}] cannot be null.", nameof(events));
            var expectedSeq = i + 1;

            if (current.EventSeq != expectedSeq)
            {
                throw new ArgumentException(
                    $"EventSeq must be strictly contiguous 1..N. Expected EventSeq {expectedSeq} at index {i}, but found {current.EventSeq}.",
                    nameof(events));
            }

            if (!seenEventIds.Add(current.EventId))
            {
                throw new ArgumentException(
                    $"Events cannot contain duplicate EventId '{current.EventId}'.",
                    nameof(events));
            }

            var currentInstantUtc = current.Timestamp.ToUniversalTime();
            if (previousInstantUtc.HasValue && currentInstantUtc < previousInstantUtc.Value)
            {
                throw new ArgumentException(
                    $"Event timestamps must be monotonic non-decreasing. EventSeq {current.EventSeq} ('{current.Timestamp:O}') precedes previous event ('{previousInstantUtc.Value:O}').",
                    nameof(events));
            }

            if (current.AnomalyInputTimestamp is not null)
            {
                Component3WorkflowProjectionEvent.ValidateNaiveIso8601Timestamp(
                    current.AnomalyInputTimestamp,
                    out var parsedNaive);
                if (previousNaive.HasValue && parsedNaive < previousNaive.Value)
                {
                    throw new ArgumentException(
                        $"Event AnomalyInputTimestamp values must be monotonic non-decreasing. EventSeq {current.EventSeq} ('{current.AnomalyInputTimestamp}') precedes previous event.",
                        nameof(events));
                }

                previousNaive = parsedNaive;
            }

            previousInstantUtc = currentInstantUtc;
            copy.Add(current);
        }

        return new ReadOnlyCollection<Component3WorkflowProjectionEvent>(copy);
    }

    private static void ValidateTimingBoundaryEvidence(
        TimingBoundaryEvidence evidence,
        string expectedCaseId,
        Guid expectedWorkflowRunId,
        string paramName)
    {
        ArgumentNullException.ThrowIfNull(evidence, paramName);

        if (!Enum.IsDefined(evidence.Status))
        {
            throw new ArgumentOutOfRangeException(paramName, $"{paramName}.Status must be a defined TimingBoundaryEvidenceStatus value.");
        }

        if (string.IsNullOrWhiteSpace(evidence.Source) ||
            string.IsNullOrWhiteSpace(evidence.SourceReference))
        {
            throw new ArgumentException(
                $"{paramName} must identify a non-blank Source and SourceReference.",
                paramName);
        }

        if (!string.Equals(evidence.CaseId, expectedCaseId, StringComparison.Ordinal) ||
            evidence.WorkflowRunId != expectedWorkflowRunId)
        {
            throw new ArgumentException(
                $"{paramName} CaseId and WorkflowRunId must match the workflow projection.",
                paramName);
        }

        if (evidence.Status == TimingBoundaryEvidenceStatus.Confirmed && !evidence.OccurredAt.HasValue)
        {
            throw new ArgumentException($"{paramName}.OccurredAt is required when Status is Confirmed.", paramName);
        }

        if (evidence.OccurredAt.HasValue && evidence.OccurredAt.Value == default)
        {
            throw new ArgumentException($"{paramName}.OccurredAt must be an explicit offset-aware instant.", paramName);
        }

        if (evidence.Status == TimingBoundaryEvidenceStatus.Unavailable && evidence.OccurredAt.HasValue)
        {
            throw new ArgumentException($"{paramName}.OccurredAt must be null when Status is Unavailable.", paramName);
        }
    }

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

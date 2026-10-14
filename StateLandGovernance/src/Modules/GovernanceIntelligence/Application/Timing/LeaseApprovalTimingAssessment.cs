using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;

namespace StateLandGovernance.GovernanceIntelligence.Application.Timing;

/// <summary>
/// Closed vocabulary for persisted lease-approval handover timing states.
/// </summary>
public enum LeaseApprovalTimingState
{
    WithinExpectedTwoCalendarMonths = 1,
    ExceededExpectedTwoCalendarMonths = 2,
    OngoingWithinExpectedTwoCalendarMonths = 3,
    OngoingExceededExpectedTwoCalendarMonths = 4,
    UnavailableEvidence = 5
}

/// <summary>
/// Immutable persisted lease-approval handover timing assessment record.
/// <para>
/// Governance Boundary: The two-calendar-month expectation is a Land Commissioner expert-reported operational
/// expectation and is explicitly NOT a verified statutory deadline, legal compliance rule, misconduct finding,
/// or automatic referral rule.
/// </para>
/// </summary>
public sealed record LeaseApprovalTimingAssessment
{
    public const string RequiredColomboTimeZone = "Asia/Colombo";
    public const int ExpectedCalendarMonths = 2;
    public const int MaxCaseIdLength = 128;
    public const int MaxPolicyVersionLength = 128;
    public const int MaxTimeZoneLength = 64;
    public const int MaxEvidenceStateLength = 128;
    public const int MaxAdvisoryNoteLength = 1024;
    private static readonly Regex Sha256HexRegex = new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public LeaseApprovalTimingAssessment(
        Guid assessmentId,
        Guid workflowRunId,
        string caseId,
        Guid? workflowProjectionId,
        string policyVersion,
        string timeZoneId,
        DateOnly assessmentDateColombo,
        DateOnly? completeProposalRoadmapStartDate,
        DateOnly? expectedApprovalHandoverDate,
        DateOnly? approvalHandoverDate,
        int? daysElapsed,
        int? daysBeyondExpected,
        LeaseApprovalTimingState timingState,
        string evidenceState,
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
        PolicyVersion = RequireNormalized(policyVersion, nameof(policyVersion), MaxPolicyVersionLength);
        EvidenceState = RequireNormalized(evidenceState, nameof(evidenceState), MaxEvidenceStateLength);
        AdvisoryNote = RequireNormalized(advisoryNote, nameof(advisoryNote), MaxAdvisoryNoteLength);

        if (string.IsNullOrWhiteSpace(timeZoneId) ||
            !string.Equals(timeZoneId.Trim(), RequiredColomboTimeZone, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"TimeZoneId must equal '{RequiredColomboTimeZone}'.",
                nameof(timeZoneId));
        }

        if (assessmentDateColombo == default)
        {
            throw new ArgumentException("AssessmentDateColombo must be a valid calendar date.", nameof(assessmentDateColombo));
        }

        if (!Enum.IsDefined(timingState))
        {
            throw new ArgumentOutOfRangeException(nameof(timingState), "TimingState must be a defined value.");
        }

        if (completeProposalRoadmapStartDate.HasValue)
        {
            var expected = completeProposalRoadmapStartDate.Value.AddMonths(ExpectedCalendarMonths);
            if (!expectedApprovalHandoverDate.HasValue || expectedApprovalHandoverDate.Value != expected)
            {
                throw new ArgumentException(
                    $"When CompleteProposalRoadmapStartDate is present ('{completeProposalRoadmapStartDate.Value:yyyy-MM-dd}'), " +
                    $"ExpectedApprovalHandoverDate must equal '{expected:yyyy-MM-dd}' (AddMonths({ExpectedCalendarMonths})).",
                    nameof(expectedApprovalHandoverDate));
            }
        }
        else if (expectedApprovalHandoverDate.HasValue)
        {
            throw new ArgumentException(
                "ExpectedApprovalHandoverDate must be null when CompleteProposalRoadmapStartDate is null.",
                nameof(expectedApprovalHandoverDate));
        }

        if (completeProposalRoadmapStartDate.HasValue &&
            approvalHandoverDate.HasValue &&
            approvalHandoverDate.Value < completeProposalRoadmapStartDate.Value)
        {
            throw new ArgumentException(
                "ApprovalHandoverDate cannot precede CompleteProposalRoadmapStartDate.",
                nameof(approvalHandoverDate));
        }

        if (daysElapsed.HasValue && daysElapsed.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysElapsed), "DaysElapsed cannot be negative when present.");
        }

        if (daysBeyondExpected.HasValue && daysBeyondExpected.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysBeyondExpected), "DaysBeyondExpected cannot be negative when present.");
        }

        if (timingState == LeaseApprovalTimingState.UnavailableEvidence)
        {
            if (daysBeyondExpected.HasValue)
            {
                throw new ArgumentException(
                    "When TimingState is UnavailableEvidence, DaysBeyondExpected must be null.",
                    nameof(daysBeyondExpected));
            }
        }
        else
        {
            if (!completeProposalRoadmapStartDate.HasValue || !expectedApprovalHandoverDate.HasValue)
            {
                throw new ArgumentException(
                    "CompleteProposalRoadmapStartDate and ExpectedApprovalHandoverDate are required when TimingState is not UnavailableEvidence.",
                    nameof(completeProposalRoadmapStartDate));
            }

            if (!daysBeyondExpected.HasValue)
            {
                throw new ArgumentException(
                    "DaysBeyondExpected is required when TimingState is not UnavailableEvidence.",
                    nameof(daysBeyondExpected));
            }
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
        TimeZoneId = RequiredColomboTimeZone;
        AssessmentDateColombo = assessmentDateColombo;
        CompleteProposalRoadmapStartDate = completeProposalRoadmapStartDate;
        ExpectedApprovalHandoverDate = expectedApprovalHandoverDate;
        ApprovalHandoverDate = approvalHandoverDate;
        DaysElapsed = daysElapsed;
        DaysBeyondExpected = daysBeyondExpected;
        TimingState = timingState;
        InputSha256 = inputSha256;
        Provenance = provenance;
        AssessedAtUtc = assessedAtUtc;
    }

    public Guid AssessmentId { get; }
    public Guid WorkflowRunId { get; }
    public string CaseId { get; }
    public Guid? WorkflowProjectionId { get; }
    public string PolicyVersion { get; }
    public string TimeZoneId { get; }
    public DateOnly AssessmentDateColombo { get; }
    public DateOnly? CompleteProposalRoadmapStartDate { get; }
    public DateOnly? ExpectedApprovalHandoverDate { get; }
    public DateOnly? ApprovalHandoverDate { get; }
    public int? DaysElapsed { get; }
    public int? DaysBeyondExpected { get; }
    public LeaseApprovalTimingState TimingState { get; }
    public string EvidenceState { get; }
    public string AdvisoryNote { get; }
    public string InputSha256 { get; }
    public GovernanceInputProvenance Provenance { get; }
    public DateTimeOffset AssessedAtUtc { get; }

    /// <summary>
    /// Creates a persisted <see cref="LeaseApprovalTimingAssessment"/> record from a deterministic
    /// <see cref="LeaseApprovalTimingAssessmentResult"/> produced by <see cref="ILeaseApprovalTimingCalculator"/>.
    /// </summary>
    public static LeaseApprovalTimingAssessment FromCalculationResult(
        Guid assessmentId,
        LeaseApprovalTimingAssessmentResult result,
        Guid? workflowProjectionId,
        string inputSha256,
        GovernanceInputProvenance provenance,
        DateTimeOffset assessedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(provenance);

        LeaseApprovalTimingState timingState;
        int? daysElapsed = null;
        string evidenceState;

        if (result.Availability == LeaseApprovalTimingAvailability.Unavailable)
        {
            timingState = LeaseApprovalTimingState.UnavailableEvidence;
            evidenceState = result.UnavailabilityReason.ToString();
        }
        else
        {
            var relevantDate = result.WorkflowState == LeaseApprovalWorkflowState.Completed
                ? result.ActualApprovalHandoverDate!.Value
                : result.AssessmentDate;
            daysElapsed = Math.Max(0, relevantDate.DayNumber - result.CompleteProposalRoadmapStartDate!.Value.DayNumber);
            evidenceState = "Confirmed";

            timingState = (result.WorkflowState, result.ExpectationState) switch
            {
                (LeaseApprovalWorkflowState.Completed, LeaseApprovalTimingExpectationState.WithinExpectation) =>
                    LeaseApprovalTimingState.WithinExpectedTwoCalendarMonths,
                (LeaseApprovalWorkflowState.Completed, LeaseApprovalTimingExpectationState.ExceededExpectation) =>
                    LeaseApprovalTimingState.ExceededExpectedTwoCalendarMonths,
                (LeaseApprovalWorkflowState.Ongoing, LeaseApprovalTimingExpectationState.WithinExpectation) =>
                    LeaseApprovalTimingState.OngoingWithinExpectedTwoCalendarMonths,
                (LeaseApprovalWorkflowState.Ongoing, LeaseApprovalTimingExpectationState.ExceededExpectation) =>
                    LeaseApprovalTimingState.OngoingExceededExpectedTwoCalendarMonths,
                _ => throw new InvalidOperationException(
                    $"Unsupported timing calculation state combination: WorkflowState={result.WorkflowState}, ExpectationState={result.ExpectationState}.")
            };
        }

        return new LeaseApprovalTimingAssessment(
            assessmentId: assessmentId,
            workflowRunId: result.WorkflowRunId,
            caseId: result.CaseId,
            workflowProjectionId: workflowProjectionId,
            policyVersion: result.TimingPolicyVersion,
            timeZoneId: RequiredColomboTimeZone,
            assessmentDateColombo: result.AssessmentDate,
            completeProposalRoadmapStartDate: result.CompleteProposalRoadmapStartDate,
            expectedApprovalHandoverDate: result.ExpectedApprovalHandoverDate,
            approvalHandoverDate: result.ActualApprovalHandoverDate,
            daysElapsed: daysElapsed,
            daysBeyondExpected: result.CalendarDaysBeyondExpectedDate,
            timingState: timingState,
            evidenceState: evidenceState,
            advisoryNote: result.ExpertReportedProvenanceNote,
            inputSha256: inputSha256,
            provenance: provenance,
            assessedAtUtc: assessedAtUtc);
    }

    /// <summary>
    /// Computes a deterministic canonical SHA-256 digest over a <see cref="LeaseApprovalTimingAssessmentInput"/> and provenance.
    /// </summary>
    public static string ComputeCanonicalInputSha256(
        LeaseApprovalTimingAssessmentInput input,
        GovernanceInputProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(provenance);

        var builder = new StringBuilder();
        builder.Append("lease_timing_input_v1|");
        builder.Append(input.WorkflowRunId.ToString("D", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(input.CaseId.Trim()).Append('|');
        builder.Append(input.WorkflowState).Append('|');
        builder.Append(input.AssessmentAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('|');
        AppendEvidence(builder, input.CompleteProposalRoadmapStart);
        builder.Append('|');
        AppendEvidence(builder, input.ApprovalHandover);
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

    private static void AppendEvidence(StringBuilder builder, TimingBoundaryEvidence? evidence)
    {
        if (evidence is null)
        {
            builder.Append("null");
            return;
        }

        builder.Append(evidence.Status).Append(':')
            .Append(evidence.OccurredAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(':')
            .Append(evidence.Source.Trim()).Append(':')
            .Append(evidence.SourceReference.Trim());
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

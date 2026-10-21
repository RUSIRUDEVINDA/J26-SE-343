namespace StateLandGovernance.GovernanceIntelligence.Application.Timing;

/// <summary>
/// Deterministic calendar-date assessment for an expert-reported expected
/// lease-approval handover duration. It is not a legal compliance rule.
/// </summary>
public sealed class LeaseApprovalTimingCalculator : ILeaseApprovalTimingCalculator
{
    public const string PolicyVersion = "lease-approval-handover-asia-colombo-calendar-months-v1";

    public const string ExpertReportedProvenanceNote =
        "Land Commissioner expert-reported expectation: approval handover is typically within two calendar months of the confirmed complete-proposal roadmap start. This is not a verified statutory deadline, legal rule, operational SLA, misconduct finding, or referral rule.";

    private const string ColomboTimeZoneId = "Asia/Colombo";
    private readonly TimeZoneInfo _colomboTimeZone;

    public LeaseApprovalTimingCalculator()
    {
        _colomboTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ColomboTimeZoneId);
    }

    public LeaseApprovalTimingAssessmentResult Assess(LeaseApprovalTimingAssessmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateIdentity(input.CaseId, input.WorkflowRunId);

        if (!Enum.IsDefined(input.WorkflowState))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "WorkflowState is not defined.");
        }

        if (input.AssessmentAt == default)
        {
            throw new ArgumentException("AssessmentAt must be an explicit offset-aware instant.", nameof(input));
        }

        ValidateEvidence(
            input.CompleteProposalRoadmapStart,
            input.CaseId,
            input.WorkflowRunId,
            nameof(input.CompleteProposalRoadmapStart));
        ValidateEvidence(
            input.ApprovalHandover,
            input.CaseId,
            input.WorkflowRunId,
            nameof(input.ApprovalHandover));

        var startInstant = ConfirmedInstant(input.CompleteProposalRoadmapStart);
        var handoverInstant = ConfirmedInstant(input.ApprovalHandover);
        var suppliedStartInstant = input.CompleteProposalRoadmapStart?.OccurredAt;
        var suppliedHandoverInstant = input.ApprovalHandover?.OccurredAt;

        if (suppliedStartInstant.HasValue && input.AssessmentAt < suppliedStartInstant.Value)
        {
            throw new ArgumentException(
                "AssessmentAt cannot be before the confirmed complete-proposal roadmap start.",
                nameof(input));
        }

        if (suppliedHandoverInstant.HasValue && input.AssessmentAt < suppliedHandoverInstant.Value)
        {
            throw new ArgumentException(
                "A confirmed approval handover cannot be later than AssessmentAt.",
                nameof(input));
        }

        if (suppliedStartInstant.HasValue &&
            suppliedHandoverInstant.HasValue &&
            suppliedHandoverInstant.Value < suppliedStartInstant.Value)
        {
            throw new ArgumentException(
                "A confirmed approval handover cannot be before the confirmed roadmap start.",
                nameof(input));
        }

        if (input.WorkflowState == LeaseApprovalWorkflowState.Ongoing && handoverInstant.HasValue)
        {
            throw new ArgumentException(
                "An ongoing workflow cannot contain a confirmed approval-handover timestamp.",
                nameof(input));
        }

        var assessmentDate = ToColomboDate(input.AssessmentAt);
        DateOnly? startDate = startInstant.HasValue ? ToColomboDate(startInstant.Value) : null;
        var expectedDate = startDate?.AddMonths(2);
        DateOnly? actualHandoverDate = handoverInstant.HasValue
            ? ToColomboDate(handoverInstant.Value)
            : null;

        var unavailableReason = GetUnavailabilityReason(input);
        if (unavailableReason != LeaseApprovalTimingUnavailabilityReason.None)
        {
            return new LeaseApprovalTimingAssessmentResult(
                input.CaseId,
                input.WorkflowRunId,
                startDate,
                expectedDate,
                actualHandoverDate,
                assessmentDate,
                input.WorkflowState,
                LeaseApprovalTimingAvailability.Unavailable,
                LeaseApprovalTimingExpectationState.Unknown,
                null,
                unavailableReason,
                PolicyVersion,
                ExpertReportedProvenanceNote,
                input.CompleteProposalRoadmapStart,
                input.ApprovalHandover);
        }

        var relevantDate = input.WorkflowState == LeaseApprovalWorkflowState.Completed
            ? actualHandoverDate!.Value
            : assessmentDate;
        var calendarDaysBeyond = Math.Max(0, relevantDate.DayNumber - expectedDate!.Value.DayNumber);
        var expectationState = relevantDate > expectedDate.Value
            ? LeaseApprovalTimingExpectationState.ExceededExpectation
            : LeaseApprovalTimingExpectationState.WithinExpectation;

        return new LeaseApprovalTimingAssessmentResult(
            input.CaseId,
            input.WorkflowRunId,
            startDate,
            expectedDate,
            actualHandoverDate,
            assessmentDate,
            input.WorkflowState,
            LeaseApprovalTimingAvailability.Available,
            expectationState,
            calendarDaysBeyond,
            LeaseApprovalTimingUnavailabilityReason.None,
            PolicyVersion,
            ExpertReportedProvenanceNote,
            input.CompleteProposalRoadmapStart,
            input.ApprovalHandover);
    }

    private DateOnly ToColomboDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _colomboTimeZone).DateTime);

    private static DateTimeOffset? ConfirmedInstant(TimingBoundaryEvidence? evidence) =>
        evidence?.Status == TimingBoundaryEvidenceStatus.Confirmed
            ? evidence.OccurredAt
            : null;

    private static LeaseApprovalTimingUnavailabilityReason GetUnavailabilityReason(
        LeaseApprovalTimingAssessmentInput input)
    {
        if (input.CompleteProposalRoadmapStart is null ||
            input.CompleteProposalRoadmapStart.Status == TimingBoundaryEvidenceStatus.Unavailable)
        {
            return LeaseApprovalTimingUnavailabilityReason.RoadmapStartUnavailable;
        }

        if (input.CompleteProposalRoadmapStart.Status == TimingBoundaryEvidenceStatus.Unconfirmed)
        {
            return LeaseApprovalTimingUnavailabilityReason.RoadmapStartUnconfirmed;
        }

        if (input.WorkflowState == LeaseApprovalWorkflowState.Completed)
        {
            if (input.ApprovalHandover is null ||
                input.ApprovalHandover.Status == TimingBoundaryEvidenceStatus.Unavailable)
            {
                return LeaseApprovalTimingUnavailabilityReason.ApprovalHandoverUnavailable;
            }

            if (input.ApprovalHandover.Status == TimingBoundaryEvidenceStatus.Unconfirmed)
            {
                return LeaseApprovalTimingUnavailabilityReason.ApprovalHandoverUnconfirmed;
            }
        }
        else if (input.ApprovalHandover?.Status == TimingBoundaryEvidenceStatus.Unconfirmed)
        {
            return LeaseApprovalTimingUnavailabilityReason.ApprovalHandoverUnconfirmed;
        }

        return LeaseApprovalTimingUnavailabilityReason.None;
    }

    private static void ValidateIdentity(string caseId, Guid workflowRunId)
    {
        if (string.IsNullOrWhiteSpace(caseId) || !string.Equals(caseId, caseId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("CaseId must be nonblank and have no surrounding whitespace.", nameof(caseId));
        }

        if (workflowRunId == Guid.Empty)
        {
            throw new ArgumentException("WorkflowRunId cannot be empty.", nameof(workflowRunId));
        }
    }

    private static void ValidateEvidence(
        TimingBoundaryEvidence? evidence,
        string expectedCaseId,
        Guid expectedWorkflowRunId,
        string parameterName)
    {
        if (evidence is null)
        {
            return;
        }

        if (!Enum.IsDefined(evidence.Status))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Boundary evidence status is not defined.");
        }

        if (string.IsNullOrWhiteSpace(evidence.Source) ||
            string.IsNullOrWhiteSpace(evidence.SourceReference))
        {
            throw new ArgumentException(
                "Boundary evidence must identify a nonblank source and source reference.",
                parameterName);
        }

        if (!string.Equals(evidence.CaseId, expectedCaseId, StringComparison.Ordinal) ||
            evidence.WorkflowRunId != expectedWorkflowRunId)
        {
            throw new ArgumentException(
                "Boundary evidence CaseId and WorkflowRunId must match the assessment exactly.",
                parameterName);
        }

        if (evidence.Status == TimingBoundaryEvidenceStatus.Confirmed && !evidence.OccurredAt.HasValue)
        {
            throw new ArgumentException(
                "Confirmed boundary evidence must include an offset-aware timestamp.",
                parameterName);
        }

        if (evidence.OccurredAt.HasValue && evidence.OccurredAt.Value == default)
        {
            throw new ArgumentException(
                "A supplied boundary timestamp must be an explicit offset-aware instant.",
                parameterName);
        }

        if (evidence.Status == TimingBoundaryEvidenceStatus.Unavailable && evidence.OccurredAt.HasValue)
        {
            throw new ArgumentException(
                "Unavailable boundary evidence cannot include a timestamp.",
                parameterName);
        }
    }
}

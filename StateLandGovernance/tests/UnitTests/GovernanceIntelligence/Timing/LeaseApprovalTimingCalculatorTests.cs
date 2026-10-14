using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.Timing;

public sealed class LeaseApprovalTimingCalculatorTests
{
    private const string CaseId = "CASE-FICTIONAL-001";
    private static readonly Guid WorkflowRunId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly TimeSpan ColomboOffset = TimeSpan.FromHours(5.5);

    private readonly LeaseApprovalTimingCalculator _calculator = new();

    [Fact]
    public void Assess_OrdinaryTwoMonthAddition_ReturnsCalendarMonthExpectation()
    {
        var input = OngoingInput(
            AtColombo(2026, 5, 15, 9),
            AtColombo(2026, 7, 14, 17));

        var result = _calculator.Assess(input);

        Assert.Equal(new DateOnly(2026, 5, 15), result.CompleteProposalRoadmapStartDate);
        Assert.Equal(new DateOnly(2026, 7, 15), result.ExpectedApprovalHandoverDate);
        Assert.Equal(LeaseApprovalTimingExpectationState.WithinExpectation, result.ExpectationState);
        Assert.Equal(0, result.CalendarDaysBeyondExpectedDate);
    }

    [Theory]
    [InlineData(2023, 12, 31, 2024, 2, 29)]
    [InlineData(2024, 12, 31, 2025, 2, 28)]
    public void Assess_DecemberMonthEnd_ClampsToLeapOrNonLeapFebruary(
        int startYear,
        int startMonth,
        int startDay,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var start = AtColombo(startYear, startMonth, startDay, 8);
        var input = OngoingInput(start, start.AddMonths(1));

        var result = _calculator.Assess(input);

        Assert.Equal(
            new DateOnly(expectedYear, expectedMonth, expectedDay),
            result.ExpectedApprovalHandoverDate);
    }

    [Fact]
    public void Assess_January31_ReturnsMarch31AndNotSixtyDayApproximation()
    {
        var input = OngoingInput(
            AtColombo(2025, 1, 31, 8),
            AtColombo(2025, 3, 1, 8));

        var result = _calculator.Assess(input);

        var expected = Assert.IsType<DateOnly>(result.ExpectedApprovalHandoverDate);
        var start = Assert.IsType<DateOnly>(result.CompleteProposalRoadmapStartDate);
        Assert.Equal(new DateOnly(2025, 3, 31), expected);
        Assert.Equal(59, expected.DayNumber - start.DayNumber);
    }

    [Theory]
    [InlineData(15, LeaseApprovalTimingExpectationState.WithinExpectation, 0)]
    [InlineData(16, LeaseApprovalTimingExpectationState.ExceededExpectation, 1)]
    public void Assess_ExpectedDateAndNextDay_UsesStrictAfterComparison(
        int handoverDay,
        LeaseApprovalTimingExpectationState expectedState,
        int expectedDaysBeyond)
    {
        var input = CompletedInput(
            AtColombo(2026, 5, 15, 9),
            AtColombo(2026, 7, handoverDay, 9),
            AtColombo(2026, 7, 20, 9));

        var result = _calculator.Assess(input);

        Assert.Equal(expectedState, result.ExpectationState);
        Assert.Equal(expectedDaysBeyond, result.CalendarDaysBeyondExpectedDate);
    }

    [Fact]
    public void Assess_CompletedAndOngoingRuns_UseDifferentRelevantDates()
    {
        var start = AtColombo(2026, 1, 10, 9);
        var assessment = AtColombo(2026, 4, 15, 9);
        var completed = CompletedInput(start, AtColombo(2026, 3, 10, 12), assessment);
        var ongoing = OngoingInput(start, assessment);

        var completedResult = _calculator.Assess(completed);
        var ongoingResult = _calculator.Assess(ongoing);

        Assert.Equal(LeaseApprovalWorkflowState.Completed, completedResult.WorkflowState);
        Assert.Equal(new DateOnly(2026, 3, 10), completedResult.ActualApprovalHandoverDate);
        Assert.Equal(LeaseApprovalTimingExpectationState.WithinExpectation, completedResult.ExpectationState);
        Assert.Equal(LeaseApprovalWorkflowState.Ongoing, ongoingResult.WorkflowState);
        Assert.Null(ongoingResult.ActualApprovalHandoverDate);
        Assert.Equal(LeaseApprovalTimingExpectationState.ExceededExpectation, ongoingResult.ExpectationState);
        Assert.Equal(36, ongoingResult.CalendarDaysBeyondExpectedDate);
    }

    [Fact]
    public void Assess_MissingRoadmapStart_ReturnsExplicitUnavailableResult()
    {
        var input = new LeaseApprovalTimingAssessmentInput(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Ongoing,
            null,
            null,
            AtColombo(2026, 7, 15, 9));

        var result = _calculator.Assess(input);

        Assert.Equal(LeaseApprovalTimingAvailability.Unavailable, result.Availability);
        Assert.Equal(LeaseApprovalTimingExpectationState.Unknown, result.ExpectationState);
        Assert.Equal(
            LeaseApprovalTimingUnavailabilityReason.RoadmapStartUnavailable,
            result.UnavailabilityReason);
        Assert.Null(result.ExpectedApprovalHandoverDate);
        Assert.Null(result.CalendarDaysBeyondExpectedDate);
    }

    [Fact]
    public void Assess_UnconfirmedRoadmapStart_ReturnsExplicitUnavailableResult()
    {
        var input = new LeaseApprovalTimingAssessmentInput(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Ongoing,
            Evidence(
                "roadmap-start",
                AtColombo(2026, 5, 15, 9),
                TimingBoundaryEvidenceStatus.Unconfirmed),
            null,
            AtColombo(2026, 7, 15, 9));

        var result = _calculator.Assess(input);

        Assert.Equal(LeaseApprovalTimingAvailability.Unavailable, result.Availability);
        Assert.Equal(LeaseApprovalTimingExpectationState.Unknown, result.ExpectationState);
        Assert.Equal(
            LeaseApprovalTimingUnavailabilityReason.RoadmapStartUnconfirmed,
            result.UnavailabilityReason);
        Assert.Null(result.CompleteProposalRoadmapStartDate);
        Assert.Null(result.CalendarDaysBeyondExpectedDate);
    }

    [Fact]
    public void Assess_UnconfirmedCompletedHandover_ReturnsExplicitUnavailableResult()
    {
        var start = AtColombo(2026, 5, 15, 9);
        var input = new LeaseApprovalTimingAssessmentInput(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Completed,
            Confirmed("roadmap-start", start),
            Evidence(
                "approval-handover",
                AtColombo(2026, 7, 15, 9),
                TimingBoundaryEvidenceStatus.Unconfirmed),
            AtColombo(2026, 7, 16, 9));

        var result = _calculator.Assess(input);

        Assert.Equal(LeaseApprovalTimingAvailability.Unavailable, result.Availability);
        Assert.Equal(LeaseApprovalTimingExpectationState.Unknown, result.ExpectationState);
        Assert.Equal(
            LeaseApprovalTimingUnavailabilityReason.ApprovalHandoverUnconfirmed,
            result.UnavailabilityReason);
        Assert.Null(result.ActualApprovalHandoverDate);
        Assert.Null(result.CalendarDaysBeyondExpectedDate);
    }

    [Fact]
    public void Assess_CompletedWithoutHandover_ReturnsExplicitUnavailableResult()
    {
        var input = new LeaseApprovalTimingAssessmentInput(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Completed,
            Confirmed("roadmap-start", AtColombo(2026, 5, 15, 9)),
            null,
            AtColombo(2026, 7, 16, 9));

        var result = _calculator.Assess(input);

        Assert.Equal(
            LeaseApprovalTimingUnavailabilityReason.ApprovalHandoverUnavailable,
            result.UnavailabilityReason);
        Assert.Equal(LeaseApprovalTimingExpectationState.Unknown, result.ExpectationState);
    }

    [Theory]
    [InlineData("handover-before-start")]
    [InlineData("assessment-before-start")]
    [InlineData("handover-after-assessment")]
    public void Assess_ContradictoryChronology_ThrowsArgumentException(string defect)
    {
        var start = AtColombo(2026, 5, 15, 9);
        var handover = AtColombo(2026, 7, 10, 9);
        var assessment = AtColombo(2026, 7, 11, 9);

        if (defect == "handover-before-start")
        {
            handover = AtColombo(2026, 5, 14, 9);
        }
        else if (defect == "assessment-before-start")
        {
            assessment = AtColombo(2026, 5, 14, 9);
        }
        else
        {
            assessment = AtColombo(2026, 7, 9, 9);
        }

        var input = CompletedInput(start, handover, assessment);

        Assert.Throws<ArgumentException>(() => _calculator.Assess(input));
    }

    [Fact]
    public void Assess_MismatchedWorkflowRunLinkage_ThrowsArgumentException()
    {
        var mismatchedStart = Confirmed(
            "roadmap-start",
            AtColombo(2026, 5, 15, 9)) with
        {
            WorkflowRunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
        };
        var input = new LeaseApprovalTimingAssessmentInput(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Ongoing,
            mismatchedStart,
            null,
            AtColombo(2026, 6, 15, 9));

        Assert.Throws<ArgumentException>(() => _calculator.Assess(input));
    }

    [Fact]
    public void Assess_UtcInstantsCrossingColomboMidnight_UsesColomboDates()
    {
        var startUtc = new DateTimeOffset(2026, 1, 31, 20, 0, 0, TimeSpan.Zero);
        var assessmentUtc = new DateTimeOffset(2026, 4, 1, 18, 31, 0, TimeSpan.Zero);
        var input = OngoingInput(startUtc, assessmentUtc);

        var result = _calculator.Assess(input);

        Assert.Equal(new DateOnly(2026, 2, 1), result.CompleteProposalRoadmapStartDate);
        Assert.Equal(new DateOnly(2026, 4, 1), result.ExpectedApprovalHandoverDate);
        Assert.Equal(new DateOnly(2026, 4, 2), result.AssessmentDate);
        Assert.Equal(LeaseApprovalTimingExpectationState.ExceededExpectation, result.ExpectationState);
        Assert.Equal(1, result.CalendarDaysBeyondExpectedDate);
    }

    [Fact]
    public void Assess_SameExplicitAssessmentTime_IsDeterministic()
    {
        var input = OngoingInput(
            AtColombo(2026, 5, 15, 9),
            AtColombo(2026, 7, 16, 9));

        var first = _calculator.Assess(input);
        var second = _calculator.Assess(input);

        Assert.Equal(first, second);
        Assert.Equal(LeaseApprovalTimingCalculator.PolicyVersion, first.TimingPolicyVersion);
        Assert.Contains("not a verified statutory deadline", first.ExpertReportedProvenanceNote);
    }

    [Fact]
    public void AddLeaseApprovalTimingMonitoring_RegistersSingletonCalculator()
    {
        var services = new ServiceCollection();

        services.AddLeaseApprovalTimingMonitoring();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var first = provider.GetRequiredService<ILeaseApprovalTimingCalculator>();
        var second = provider.GetRequiredService<ILeaseApprovalTimingCalculator>();
        Assert.IsType<LeaseApprovalTimingCalculator>(first);
        Assert.Same(first, second);
    }

    private static LeaseApprovalTimingAssessmentInput OngoingInput(
        DateTimeOffset start,
        DateTimeOffset assessment) =>
        new(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Ongoing,
            Confirmed("roadmap-start", start),
            null,
            assessment);

    private static LeaseApprovalTimingAssessmentInput CompletedInput(
        DateTimeOffset start,
        DateTimeOffset handover,
        DateTimeOffset assessment) =>
        new(
            CaseId,
            WorkflowRunId,
            LeaseApprovalWorkflowState.Completed,
            Confirmed("roadmap-start", start),
            Confirmed("approval-handover", handover),
            assessment);

    private static TimingBoundaryEvidence Confirmed(string reference, DateTimeOffset occurredAt) =>
        Evidence(reference, occurredAt, TimingBoundaryEvidenceStatus.Confirmed);

    private static TimingBoundaryEvidence Evidence(
        string reference,
        DateTimeOffset? occurredAt,
        TimingBoundaryEvidenceStatus status) =>
        new(
            occurredAt,
            status,
            "fictional-test-source",
            reference,
            CaseId,
            WorkflowRunId);

    private static DateTimeOffset AtColombo(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, ColomboOffset);
}

using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class EarlyGovernanceReferralOrchestrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Clear")]
    [InlineData("ReviewRequired")]
    [InlineData("InsufficientInformation")]
    public async Task DefaultPolicy_PersistsAssessmentWithoutAutomaticCommissionerReferral(string expectedStatus)
    {
        var store = new ReferralStoreFake();
        var handler = CreateScreeningHandler(store, new UnconfiguredEarlyGovernanceReferralPolicy());
        var indicators = expectedStatus switch
        {
            "Clear" => AllIndicators("VerifiedAbsent"),
            "ReviewRequired" => ReviewRequiredIndicators(),
            _ => Array.Empty<EarlyGovernanceIndicatorDto>()
        };

        var result = await handler.HandleAsync(new ScreenEarlyGovernanceCommand(
            Guid.NewGuid(), Guid.NewGuid(), "CASE-POLICY", "v1", indicators));

        Assert.Equal(expectedStatus, result.OverallStatus);
        Assert.NotNull(store.SavedAssessment);
        Assert.Null(store.SavedReferral);
    }

    [Fact]
    public async Task ExplicitReferralPolicy_PersistsAssessmentAndIntentAtomicallyBeforeReturning()
    {
        var store = new ReferralStoreFake();
        var assessmentId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var policy = new FixedReferralPolicy();
        var handler = CreateScreeningHandler(store, policy);

        await handler.HandleAsync(new ScreenEarlyGovernanceCommand(
            assessmentId, workflowRunId, "CASE-REFERRAL", "v1", AllIndicators("VerifiedAbsent")));

        var referral = Assert.IsType<EarlyGovernanceReferralIntentDto>(store.SavedReferral);
        Assert.Equal(assessmentId, referral.AssessmentId);
        Assert.Equal(assessmentId, referral.CorrelationId);
        Assert.Equal(workflowRunId, referral.WorkflowRunId);
        Assert.Equal("CASE-REFERRAL", referral.CaseId);
        Assert.Equal(Now, referral.RequestedAtUtc);
        Assert.Equal(["DOC-1", "DOC-2"], referral.EvidenceReferences);
    }

    [Fact]
    public async Task PersistenceFailure_DoesNotReturnAClassificationResult()
    {
        var store = new ReferralStoreFake { AddFailure = new InvalidOperationException("database unavailable") };
        var handler = CreateScreeningHandler(store, new FixedReferralPolicy());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new ScreenEarlyGovernanceCommand(
                Guid.NewGuid(), Guid.NewGuid(), "CASE-FAIL", "v1", AllIndicators("VerifiedAbsent"))));
    }

    [Fact]
    public async Task Contract_DeliveryFailure_RemainsVisibleAndRetryUsesSameCorrelationThenAcknowledgesOnce()
    {
        var referral = CreateStoredReferral();
        var store = new ReferralStoreFake { CurrentReferral = referral };
        var handoff = new HandoffFake(
            new EarlyGovernanceReferralHandoffResult(
                EarlyGovernanceReferralHandoffOutcome.TemporarilyUnavailable, FailureCode: "C3_UNAVAILABLE"),
            new EarlyGovernanceReferralHandoffResult(
                EarlyGovernanceReferralHandoffOutcome.Acknowledged, "COMMISSIONER-REVIEW-42"));
        var handler = new DeliverEarlyGovernanceReferralCommandHandler(store, handoff, new FixedTimeProvider(Now));

        var first = await handler.HandleAsync(new(referral.ReferralId));
        var second = await handler.HandleAsync(new(referral.ReferralId));
        var third = await handler.HandleAsync(new(referral.ReferralId));

        Assert.False(first.WorkflowTransitionConfirmed);
        Assert.Equal(EarlyGovernanceReferralDeliveryState.DeliveryFailed, first.Referral.DeliveryState);
        Assert.Equal("C3_UNAVAILABLE", first.Referral.LastFailureCode);
        Assert.True(second.WorkflowTransitionConfirmed);
        Assert.Equal(EarlyGovernanceReferralDeliveryState.Acknowledged, second.Referral.DeliveryState);
        Assert.Equal("COMMISSIONER-REVIEW-42", second.Referral.CommissionerReviewProcessReference);
        Assert.True(third.WorkflowTransitionConfirmed);
        Assert.Equal("AlreadyAcknowledged", third.OutcomeCode);
        Assert.Equal(2, handoff.Requests.Count);
        Assert.All(handoff.Requests, request => Assert.Equal(referral.CorrelationId, request.CorrelationId));
    }

    [Fact]
    public async Task Contract_ConfirmedOutcomeWithoutReviewReference_IsRecordedAsFailure()
    {
        var referral = CreateStoredReferral();
        var store = new ReferralStoreFake { CurrentReferral = referral };
        var handoff = new HandoffFake(new EarlyGovernanceReferralHandoffResult(
            EarlyGovernanceReferralHandoffOutcome.AlreadyProcessed));
        var handler = new DeliverEarlyGovernanceReferralCommandHandler(store, handoff, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new(referral.ReferralId));

        Assert.False(result.WorkflowTransitionConfirmed);
        Assert.Equal(EarlyGovernanceReferralDeliveryState.DeliveryFailed, result.Referral.DeliveryState);
        Assert.Equal("MissingCommissionerReviewProcessReference", result.Referral.LastFailureCode);
    }

    [Theory]
    [InlineData(EarlyGovernanceReferralHandoffOutcome.CaseWorkflowRunMismatch)]
    [InlineData(EarlyGovernanceReferralHandoffOutcome.WorkflowRunNotFound)]
    [InlineData(EarlyGovernanceReferralHandoffOutcome.WorkflowRunAlreadyEnded)]
    public async Task Contract_StaleOrMismatchedWorkflowRun_IsNeverReportedAsSuccessful(
        EarlyGovernanceReferralHandoffOutcome outcome)
    {
        var referral = CreateStoredReferral();
        var store = new ReferralStoreFake { CurrentReferral = referral };
        var handler = new DeliverEarlyGovernanceReferralCommandHandler(
            store, new HandoffFake(new EarlyGovernanceReferralHandoffResult(outcome)),
            new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new(referral.ReferralId));

        Assert.False(result.WorkflowTransitionConfirmed);
        Assert.Equal(EarlyGovernanceReferralDeliveryState.DeliveryFailed, result.Referral.DeliveryState);
        Assert.Equal(outcome.ToString(), result.Referral.LastFailureCode);
    }

    private static ScreenEarlyGovernanceCommandHandler CreateScreeningHandler(
        IEarlyGovernanceScreeningStore store,
        IEarlyGovernanceReferralPolicy policy) =>
        new(new EarlyGovernanceScreeningEngine(), store, policy, new FixedTimeProvider(Now));

    private static EarlyGovernanceIndicatorDto[] AllIndicators(string state) =>
    [
        Indicator("LegalDispute", state), Indicator("UnauthorizedOccupation", state),
        Indicator("UnauthorizedConstruction", state), Indicator("FamilyOrInheritanceClaim", state),
        Indicator("MultipleClaimants", state), Indicator("UnresolvedObjection", state),
        Indicator("PreviousIllegalLandActivity", state)
    ];

    private static EarlyGovernanceIndicatorDto[] ReviewRequiredIndicators()
    {
        var indicators = AllIndicators("VerifiedAbsent");
        indicators[0] = Indicator("LegalDispute", "VerifiedPresent");
        return indicators;
    }

    private static EarlyGovernanceIndicatorDto Indicator(string type, string state) =>
        new(type, state, $"DOC-{type}", Now);

    private static StoredEarlyGovernanceReferralDto CreateStoredReferral() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CASE-DELIVERY", Guid.NewGuid(),
            "EG_REFERRAL_REQUIRED", "Approved policy requires Commissioner review.", ["DOC-1"], Now,
            EarlyGovernanceReferralDeliveryState.Pending, 0, null, null, null, null);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FixedReferralPolicy : IEarlyGovernanceReferralPolicy
    {
        public EarlyGovernanceReferralPolicyDecision Evaluate(EarlyGovernanceScreeningResultDto screeningResult) =>
            EarlyGovernanceReferralPolicyDecision.ReferralRequired(
                "EG_REFERRAL_REQUIRED", "Approved policy requires Commissioner review.", ["DOC-1", "DOC-2"]);
    }

    private sealed class HandoffFake(params EarlyGovernanceReferralHandoffResult[] results)
        : IEarlyGovernanceReferralHandoff
    {
        private readonly Queue<EarlyGovernanceReferralHandoffResult> _results = new(results);
        public List<EarlyGovernanceReferralHandoffRequest> Requests { get; } = [];

        public Task<EarlyGovernanceReferralHandoffResult> SendAsync(
            EarlyGovernanceReferralHandoffRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class ReferralStoreFake : IEarlyGovernanceScreeningStore
    {
        public EarlyGovernanceScreeningResultDto? SavedAssessment { get; private set; }
        public EarlyGovernanceReferralIntentDto? SavedReferral { get; private set; }
        public StoredEarlyGovernanceReferralDto? CurrentReferral { get; set; }
        public Exception? AddFailure { get; init; }

        public Task AddAsync(Guid assessmentId, DateTimeOffset createdAtUtc,
            EarlyGovernanceScreeningResultDto result, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAssessmentAsync(Guid assessmentId, Guid workflowRunId, DateTimeOffset createdAtUtc,
            EarlyGovernanceScreeningResultDto result, EarlyGovernanceReferralIntentDto? referral,
            CancellationToken cancellationToken = default)
        {
            if (AddFailure is not null) throw AddFailure;
            SavedAssessment = result;
            SavedReferral = referral;
            return Task.CompletedTask;
        }

        public Task<StoredEarlyGovernanceScreeningDto?> GetByIdAsync(Guid assessmentId,
            CancellationToken cancellationToken = default) => Task.FromResult<StoredEarlyGovernanceScreeningDto?>(null);

        public Task<StoredEarlyGovernanceReferralDto?> GetReferralByIdAsync(Guid referralId,
            CancellationToken cancellationToken = default) => Task.FromResult(CurrentReferral);

        public Task<IReadOnlyList<StoredEarlyGovernanceReferralDto>> GetReferralHistoryByCaseIdAsync(
            string caseId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredEarlyGovernanceReferralDto>>([]);

        public Task<bool> TryBeginReferralDeliveryAsync(Guid referralId, int expectedAttemptCount,
            DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken = default)
        {
            if (CurrentReferral is null || CurrentReferral.DeliveryAttemptCount != expectedAttemptCount ||
                CurrentReferral.DeliveryState == EarlyGovernanceReferralDeliveryState.Acknowledged)
                return Task.FromResult(false);
            CurrentReferral = CurrentReferral with
            {
                DeliveryState = EarlyGovernanceReferralDeliveryState.Delivering,
                DeliveryAttemptCount = expectedAttemptCount + 1,
                LastAttemptAtUtc = attemptedAtUtc,
                LastFailureCode = null
            };
            return Task.FromResult(true);
        }

        public Task MarkReferralDeliveryFailedAsync(Guid referralId, int deliveryAttemptCount, string failureCode,
            CancellationToken cancellationToken = default)
        {
            CurrentReferral = CurrentReferral! with
            {
                DeliveryState = EarlyGovernanceReferralDeliveryState.DeliveryFailed,
                LastFailureCode = failureCode
            };
            return Task.CompletedTask;
        }

        public Task MarkReferralAcknowledgedAsync(Guid referralId, int deliveryAttemptCount,
            DateTimeOffset acknowledgedAtUtc, string commissionerReviewProcessReference,
            CancellationToken cancellationToken = default)
        {
            CurrentReferral = CurrentReferral! with
            {
                DeliveryState = EarlyGovernanceReferralDeliveryState.Acknowledged,
                AcknowledgedAtUtc = acknowledgedAtUtc,
                CommissionerReviewProcessReference = commissionerReviewProcessReference,
                LastFailureCode = null
            };
            return Task.CompletedTask;
        }
    }
}

using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

public sealed record DeliverEarlyGovernanceReferralCommand(Guid ReferralId);

public sealed class DeliverEarlyGovernanceReferralCommandHandler
{
    private readonly IEarlyGovernanceScreeningStore _store;
    private readonly IEarlyGovernanceReferralHandoff _handoff;
    private readonly TimeProvider _timeProvider;

    public DeliverEarlyGovernanceReferralCommandHandler(
        IEarlyGovernanceScreeningStore store,
        IEarlyGovernanceReferralHandoff handoff,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<EarlyGovernanceReferralDeliveryResultDto> HandleAsync(
        DeliverEarlyGovernanceReferralCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ReferralId == Guid.Empty)
        {
            throw new ArgumentException("ReferralId cannot be empty.", nameof(command));
        }

        var referral = await _store.GetReferralByIdAsync(command.ReferralId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Referral '{command.ReferralId}' was not found.");

        if (referral.DeliveryState == EarlyGovernanceReferralDeliveryState.Acknowledged)
        {
            return new EarlyGovernanceReferralDeliveryResultDto(referral, true, "AlreadyAcknowledged");
        }

        var attemptedAtUtc = _timeProvider.GetUtcNow();
        var began = await _store.TryBeginReferralDeliveryAsync(
            referral.ReferralId,
            referral.DeliveryAttemptCount,
            attemptedAtUtc,
            cancellationToken).ConfigureAwait(false);

        if (!began)
        {
            var current = await _store.GetReferralByIdAsync(referral.ReferralId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Referral disappeared while beginning delivery.");
            return new EarlyGovernanceReferralDeliveryResultDto(
                current,
                current.DeliveryState == EarlyGovernanceReferralDeliveryState.Acknowledged,
                "ConcurrentDeliveryStateChanged");
        }

        int attemptCount = referral.DeliveryAttemptCount + 1;
        EarlyGovernanceReferralHandoffResult handoffResult;
        try
        {
            handoffResult = await _handoff.SendAsync(
                new EarlyGovernanceReferralHandoffRequest(
                    referral.ReferralId,
                    referral.CorrelationId,
                    referral.AssessmentId,
                    referral.CaseId,
                    referral.WorkflowRunId,
                    referral.ReasonCode,
                    referral.Reason,
                    referral.EvidenceReferences,
                    referral.RequestedAtUtc),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _store.MarkReferralDeliveryFailedAsync(
                referral.ReferralId,
                attemptCount,
                "DeliveryCancelled",
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            await _store.MarkReferralDeliveryFailedAsync(
                referral.ReferralId,
                attemptCount,
                "HandoffException",
                CancellationToken.None).ConfigureAwait(false);
            return await LoadResultAsync(referral.ReferralId, false, "HandoffException", cancellationToken).ConfigureAwait(false);
        }

        if (handoffResult.ConfirmsTransition &&
            !string.IsNullOrWhiteSpace(handoffResult.CommissionerReviewProcessReference))
        {
            await _store.MarkReferralAcknowledgedAsync(
                referral.ReferralId,
                attemptCount,
                _timeProvider.GetUtcNow(),
                handoffResult.CommissionerReviewProcessReference,
                cancellationToken).ConfigureAwait(false);
            return await LoadResultAsync(referral.ReferralId, true, handoffResult.Outcome.ToString(), cancellationToken).ConfigureAwait(false);
        }

        string failureCode = handoffResult.ConfirmsTransition
            ? "MissingCommissionerReviewProcessReference"
            : handoffResult.FailureCode ?? handoffResult.Outcome.ToString();
        await _store.MarkReferralDeliveryFailedAsync(
            referral.ReferralId,
            attemptCount,
            failureCode,
            cancellationToken).ConfigureAwait(false);
        return await LoadResultAsync(referral.ReferralId, false, failureCode, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EarlyGovernanceReferralDeliveryResultDto> LoadResultAsync(
        Guid referralId,
        bool confirmed,
        string outcomeCode,
        CancellationToken cancellationToken)
    {
        var current = await _store.GetReferralByIdAsync(referralId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Referral disappeared after delivery state was updated.");
        return new EarlyGovernanceReferralDeliveryResultDto(current, confirmed, outcomeCode);
    }
}

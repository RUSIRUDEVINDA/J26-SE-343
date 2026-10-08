using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Internal storage abstraction for early governance screening evaluation snapshots.
/// Provides append and retrieval operations for audit and evaluation history.
/// </summary>
public interface IEarlyGovernanceScreeningStore
{
    /// <summary>
    /// Persists a new early governance screening evaluation snapshot using caller-supplied assessment identity and timestamp.
    /// </summary>
    Task AddAsync(
        Guid assessmentId,
        DateTimeOffset createdAtUtc,
        EarlyGovernanceScreeningResultDto result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically appends a screening assessment and its optional referral intent.
    /// </summary>
    Task AddAssessmentAsync(
        Guid assessmentId,
        Guid workflowRunId,
        DateTimeOffset createdAtUtc,
        EarlyGovernanceScreeningResultDto result,
        EarlyGovernanceReferralIntentDto? referral,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a stored evaluation snapshot by its assessment identifier, or returns null if not found.
    /// </summary>
    Task<StoredEarlyGovernanceScreeningDto?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<StoredEarlyGovernanceReferralDto?> GetReferralByIdAsync(
        Guid referralId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredEarlyGovernanceReferralDto>> GetReferralHistoryByCaseIdAsync(
        string caseId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> TryBeginReferralDeliveryAsync(
        Guid referralId,
        int expectedAttemptCount,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkReferralDeliveryFailedAsync(
        Guid referralId,
        int deliveryAttemptCount,
        string failureCode,
        CancellationToken cancellationToken = default);

    Task MarkReferralAcknowledgedAsync(
        Guid referralId,
        int deliveryAttemptCount,
        DateTimeOffset acknowledgedAtUtc,
        string commissionerReviewProcessReference,
        CancellationToken cancellationToken = default);
}

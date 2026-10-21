using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;

namespace StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;

/// <summary>
/// Persistence abstraction for storing and retrieving consolidated governance screening runs.
/// Supports idempotent re-execution and unified case history lookup.
/// </summary>
public interface IConsolidatedGovernanceScreeningStore
{
    /// <summary>
    /// Persists a consolidated screening assessment result.
    /// </summary>
    Task SaveAsync(
        ConsolidatedGovernanceScreeningResultDto assessment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a consolidated assessment by its unique assessment ID.
    /// </summary>
    Task<ConsolidatedGovernanceScreeningResultDto?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent consolidated assessment for a given case.
    /// </summary>
    Task<ConsolidatedGovernanceScreeningResultDto?> GetLatestByCaseIdAsync(
        string caseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing assessment matching the exact input snapshot SHA-256 for idempotency.
    /// </summary>
    Task<ConsolidatedGovernanceScreeningResultDto?> FindByIdempotencyKeyAsync(
        string caseId,
        string proposalVersion,
        string snapshotSha256,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all historical screening assessment runs for a case.
    /// </summary>
    Task<IReadOnlyList<ConsolidatedGovernanceScreeningResultDto>> GetHistoryByCaseIdAsync(
        string caseId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}


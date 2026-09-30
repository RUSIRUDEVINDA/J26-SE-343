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
    /// Retrieves a stored evaluation snapshot by its assessment identifier, or returns null if not found.
    /// </summary>
    Task<StoredEarlyGovernanceScreeningDto?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);
}

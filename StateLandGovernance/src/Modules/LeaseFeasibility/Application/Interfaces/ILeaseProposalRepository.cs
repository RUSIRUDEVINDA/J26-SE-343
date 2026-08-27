using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;

namespace StateLandGovernance.LeaseFeasibility.Application.Interfaces;

/// <summary>
/// Application-layer interface for the persistence boundary of LeaseProposal entities.
/// </summary>
public interface ILeaseProposalRepository
{
    /// <summary>
    /// Retrieves a lease proposal by its unique identifier.
    /// </summary>
    Task<LeaseProposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new lease proposal to persistence.
    /// </summary>
    Task AddAsync(LeaseProposal proposal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing lease proposal in persistence.
    /// </summary>
    Task UpdateAsync(LeaseProposal proposal, CancellationToken cancellationToken = default);
}

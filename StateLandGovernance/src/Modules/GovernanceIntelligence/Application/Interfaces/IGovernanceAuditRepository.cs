using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application-layer interface for the persistence boundary of GovernanceAuditRecord entities.
/// </summary>
public interface IGovernanceAuditRepository
{
    /// <summary>
    /// Retrieves a GovernanceAuditRecord by its unique identifier.
    /// </summary>
    Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new GovernanceAuditRecord to persistence.
    /// </summary>
    Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default);
}

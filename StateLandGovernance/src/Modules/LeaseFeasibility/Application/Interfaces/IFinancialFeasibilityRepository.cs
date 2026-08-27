using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;

namespace StateLandGovernance.LeaseFeasibility.Application.Interfaces;

/// <summary>
/// Application-layer interface for the persistence boundary of FinancialFeasibilityAssessment entities.
/// </summary>
public interface IFinancialFeasibilityRepository
{
    /// <summary>
    /// Retrieves an assessment by its unique identifier.
    /// </summary>
    Task<FinancialFeasibilityAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new assessment to persistence.
    /// </summary>
    Task AddAsync(FinancialFeasibilityAssessment assessment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing assessment in persistence.
    /// </summary>
    Task UpdateAsync(FinancialFeasibilityAssessment assessment, CancellationToken cancellationToken = default);
}

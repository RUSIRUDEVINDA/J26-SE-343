using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StateLandGovernance.LeaseFeasibility.Application.Interfaces;

/// <summary>
/// Application-layer interface for retrieving historical successful lease cases for RAG optimization.
/// </summary>
public interface IHistoricalCaseRepository
{
    /// <summary>
    /// Retrieves a historical case by its unique identifier.
    /// </summary>
    Task<object?> GetHistoricalCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds historical cases matching specified criteria.
    /// </summary>
    Task<IReadOnlyList<object>> FindSimilarCasesAsync(object criteria, CancellationToken cancellationToken = default);
}

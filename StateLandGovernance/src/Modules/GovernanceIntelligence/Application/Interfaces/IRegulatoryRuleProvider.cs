using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application-layer interface to fetch active regulatory rules.
/// </summary>
public interface IRegulatoryRuleProvider
{
    /// <summary>
    /// Fetches all active regulatory rules.
    /// </summary>
    Task<IEnumerable<RegulatoryRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
}

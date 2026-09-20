using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application interface for loading active regulatory compliance rule definitions from the catalogue.
/// </summary>
public interface IComplianceRuleCatalogue
{
    /// <summary>
    /// Retrieves all active regulatory compliance rule definitions applicable for a specific evaluation timestamp.
    /// </summary>
    Task<IReadOnlyList<ComplianceRuleDefinition>> GetActiveRulesAsync(
        DateTime evaluationTimestamp,
        CancellationToken cancellationToken = default);
}

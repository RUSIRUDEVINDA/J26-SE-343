using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of the regulatory compliance rule catalogue provider for unit testing and development mode.
/// </summary>
public sealed class InMemoryComplianceRuleCatalogue : IComplianceRuleCatalogue
{
    private readonly IReadOnlyList<ComplianceRuleDefinition> _rules;

    public InMemoryComplianceRuleCatalogue(IEnumerable<ComplianceRuleDefinition>? rules = null)
    {
        _rules = rules != null
            ? new List<ComplianceRuleDefinition>(rules)
            : NpdRuleCatalogue.GetDefaultRuleDefinitions();
    }

    public Task<IReadOnlyList<ComplianceRuleDefinition>> GetActiveRulesAsync(
        DateTime evaluationTimestamp,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_rules);
    }
}

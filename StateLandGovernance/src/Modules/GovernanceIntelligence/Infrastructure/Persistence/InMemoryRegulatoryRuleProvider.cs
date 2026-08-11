using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of the IRegulatoryRuleProvider serving active regulatory rules.
/// </summary>
public sealed class InMemoryRegulatoryRuleProvider : IRegulatoryRuleProvider
{
    private readonly List<RegulatoryRule> _rules = new()
    {
        new MaxLeaseDurationRule(),
        new ZoningMatchRule(),
        new MinimumLeaseValueRule()
    };

    public Task<IEnumerable<RegulatoryRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<RegulatoryRule>>(_rules);
    }
}

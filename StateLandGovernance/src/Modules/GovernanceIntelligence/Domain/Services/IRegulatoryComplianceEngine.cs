using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service interface performing regulatory compliance assessments.
/// </summary>
public interface IRegulatoryComplianceEngine
{
    /// <summary>
    /// Evaluates the lease inputs against the provided active regulatory rules.
    /// </summary>
    ComplianceResult Evaluate(LeaseEvaluationInput input, IEnumerable<RegulatoryRule> rules);
}

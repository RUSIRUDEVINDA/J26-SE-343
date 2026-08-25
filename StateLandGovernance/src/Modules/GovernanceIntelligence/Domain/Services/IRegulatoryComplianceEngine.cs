using System;
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
    /// Evaluates the legacy lease inputs against active regulatory rules.
    /// </summary>
    ComplianceResult Evaluate(LeaseEvaluationInput input, IEnumerable<RegulatoryRule> rules);

    /// <summary>
    /// Evaluates structured proposal facts against source-backed NPD operational compliance rules.
    /// </summary>
    ComplianceResult EvaluateNpd(ProposalComplianceInput input, DateTime evaluationTimestamp);
}

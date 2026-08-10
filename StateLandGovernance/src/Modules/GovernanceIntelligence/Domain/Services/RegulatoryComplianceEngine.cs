using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service implementation of the regulatory compliance engine.
/// </summary>
public sealed class RegulatoryComplianceEngine : IRegulatoryComplianceEngine
{
    public ComplianceResult Evaluate(LeaseEvaluationInput input, IEnumerable<RegulatoryRule> rules)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (rules is null)
        {
            throw new ArgumentNullException(nameof(rules));
        }

        var violations = new List<Violation>();
        var conditions = new List<ComplianceCondition>();

        // Evaluate all active rules
        foreach (var rule in rules.Where(r => r != null && r.IsActive))
        {
            rule.Evaluate(input, violations, conditions);
        }

        // Determine outcome status based on evaluations
        ComplianceStatus status;
        if (violations.Count > 0)
        {
            status = ComplianceStatus.NonCompliant;
        }
        else if (conditions.Count > 0)
        {
            status = ComplianceStatus.Conditional;
        }
        else
        {
            status = ComplianceStatus.Compliant;
        }

        return new ComplianceResult(status, violations, conditions);
    }
}

using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Entities;

/// <summary>
/// Concrete regulatory rule asserting maximum lease durations do not exceed legal limits.
/// </summary>
public sealed class MaxLeaseDurationRule : RegulatoryRule
{
    private const int LegalLimitYears = 99;
    private const int ConditionalThresholdYears = 30;

    public MaxLeaseDurationRule() : base(
        code: "RULE_LEASE_MAX_DURATION",
        name: "Maximum Lease Duration Rule",
        description: "Enforces that lease terms cannot exceed 99 years, and terms exceeding 30 years require special authorization.",
        category: RuleCategory.LeaseTerm,
        isActive: true)
    {
    }

    public override void Evaluate(LeaseEvaluationInput input, List<Violation> violations, List<ComplianceCondition> conditions)
    {
        if (input.LeaseDurationYears <= 0)
        {
            violations.Add(new Violation(Code, "Lease duration must be greater than zero."));
            return;
        }

        if (input.LeaseDurationYears > LegalLimitYears)
        {
            violations.Add(new Violation(Code, $"Lease duration of {input.LeaseDurationYears} years exceeds the statutory limit of {LegalLimitYears} years."));
        }
        else if (input.LeaseDurationYears > ConditionalThresholdYears)
        {
            conditions.Add(new ComplianceCondition(
                $"Lease duration of {input.LeaseDurationYears} years requires explicit ministerial review and gazette notification.",
                DateTime.UtcNow.AddDays(90)));
        }
    }
}

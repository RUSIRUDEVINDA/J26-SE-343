using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Entities;

/// <summary>
/// Concrete regulatory rule asserting minimum lease financial values.
/// </summary>
public sealed class MinimumLeaseValueRule : RegulatoryRule
{
    private const decimal AbsoluteMinimumAmount = 0.01m;
    private const decimal ValuationReviewThreshold = 1000.00m;

    public MinimumLeaseValueRule() : base(
        code: "RULE_MIN_LEASE_VALUE",
        name: "Minimum Lease Value Rule",
        description: "Assures proposed lease fees meet minimal legal amounts and flags low values for valuation department verification.",
        category: RuleCategory.Financial,
        isActive: true)
    {
    }

    public override void Evaluate(LeaseEvaluationInput input, List<Violation> violations, List<ComplianceCondition> conditions)
    {
        if (input.LeaseAmount < AbsoluteMinimumAmount)
        {
            violations.Add(new Violation(Code, $"Lease amount of {input.LeaseAmount:C} is invalid or below the statutory minimum of {AbsoluteMinimumAmount:C}."));
        }
        else if (input.LeaseAmount < ValuationReviewThreshold)
        {
            conditions.Add(new ComplianceCondition(
                $"Proposed lease fee of {input.LeaseAmount:C} is below the assessment threshold of {ValuationReviewThreshold:C} and requires Chief Valuer certification.",
                DateTime.UtcNow.AddDays(45)));
        }
    }
}

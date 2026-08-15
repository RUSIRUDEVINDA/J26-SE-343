using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Entities;

/// <summary>
/// Concrete regulatory rule verifying that the proposed lease use matches local zoning parameters.
/// </summary>
public sealed class ZoningMatchRule : RegulatoryRule
{
    public ZoningMatchRule() : base(
        code: "RULE_ZONING_MATCH",
        name: "Zoning Compatibility Rule",
        description: "Validates that the proposed land use is compatible with the zoning classification of the land parcel.",
        category: RuleCategory.Zoning,
        isActive: true)
    {
    }

    public override void Evaluate(LeaseEvaluationInput input, List<Violation> violations, List<ComplianceCondition> conditions)
    {
        if (string.IsNullOrWhiteSpace(input.ZoningArea))
        {
            violations.Add(new Violation(Code, "Zoning classification cannot be empty."));
            return;
        }

        string zoning = input.ZoningArea.Trim().ToLowerInvariant();
        string proposedUse = (input.ProposedUse ?? string.Empty).Trim().ToLowerInvariant();

        if (zoning == "forestreserve" || zoning == "reserve")
        {
            if (proposedUse != "conservation" && proposedUse != "ecotourism")
            {
                violations.Add(new Violation(Code, $"Proposed use '{input.ProposedUse}' is strictly prohibited in a Forest Reserve."));
            }
        }
        else if (zoning == "residential")
        {
            if (proposedUse == "industrial")
            {
                violations.Add(new Violation(Code, "Industrial operations are prohibited in Residential zoning areas."));
            }
            else if (proposedUse == "commercial")
            {
                conditions.Add(new ComplianceCondition(
                    "Commercial operations in Residential zoning require local authority permits and environmental clearance.",
                    DateTime.UtcNow.AddDays(30)));
            }
        }
        else if (zoning == "agricultural")
        {
            if (proposedUse == "industrial" || proposedUse == "residential")
            {
                violations.Add(new Violation(Code, $"Proposed use '{input.ProposedUse}' requires zoning amendment to be allowed on Agricultural land."));
            }
        }
    }
}

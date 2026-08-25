using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Entities;

/// <summary>
/// Domain aggregate/entity representing an abstract regulatory compliance rule.
/// </summary>
public abstract class RegulatoryRule
{
    public string Code { get; }
    public string Name { get; }
    public string Description { get; }
    public RuleCategory Category { get; }
    public bool IsActive { get; }
    public RuleSourceType SourceType => RuleSourceType.ResearchConfiguration;

    protected RegulatoryRule(string code, string name, string description, RuleCategory category, bool isActive)
    {
        Code = code;
        Name = name;
        Description = description;
        Category = category;
        IsActive = isActive;
    }

    /// <summary>
    /// Evaluates the input details against the rule, appending any violations or conditions.
    /// </summary>
    public abstract void Evaluate(
        LeaseEvaluationInput input, 
        List<Violation> violations, 
        List<ComplianceCondition> conditions);
}

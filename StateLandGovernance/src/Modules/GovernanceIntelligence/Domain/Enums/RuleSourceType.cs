namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Identifies the legal, statutory, or operational authority source of a regulatory compliance rule.
/// </summary>
public enum RuleSourceType
{
    GovernmentOperationalManual,
    GovernmentCircular,
    Gazette,
    Statute,
    Regulation,
    DepartmentValidatedRule,
    ResearchConfiguration
}

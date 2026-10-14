using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Domain value object representing a loaded regulatory compliance rule definition from the catalogue.
/// </summary>
public sealed record ComplianceRuleDefinition(
    string RuleCode,
    string RuleVersion,
    string Category,
    RuleEvaluationType EvaluationType,
    string Description,
    string? CalculationKey,
    string Severity,
    bool IsBlocking,
    bool Enabled,
    RuleSourceMetadata SourceReference,
    IReadOnlyDictionary<string, string> Parameters
);

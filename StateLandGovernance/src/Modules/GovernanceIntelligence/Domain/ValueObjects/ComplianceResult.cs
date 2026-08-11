using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object holding the output results of a regulatory compliance evaluation.
/// </summary>
public sealed record ComplianceResult(
    ComplianceStatus Status,
    IReadOnlyList<Violation> Violations,
    IReadOnlyList<ComplianceCondition> Conditions
);

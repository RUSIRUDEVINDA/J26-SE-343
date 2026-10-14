using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing an active compliance prerequisite condition.
/// </summary>
public sealed record ComplianceCondition(
    string Description,
    DateTime? RequiredByDate
);

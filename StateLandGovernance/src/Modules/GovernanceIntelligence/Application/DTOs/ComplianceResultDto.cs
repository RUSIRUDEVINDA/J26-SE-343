using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// DTO representing a violation found during evaluation.
/// </summary>
public sealed record ViolationDto(string RuleCode, string Message);

/// <summary>
/// DTO representing a condition generated during evaluation.
/// </summary>
public sealed record ComplianceConditionDto(string Description, DateTime? RequiredByDate);

/// <summary>
/// DTO representing the outcome assessment of a regulatory compliance evaluation request.
/// </summary>
public sealed record ComplianceResultDto(
    string Status,
    IReadOnlyList<ViolationDto> Violations,
    IReadOnlyList<ComplianceConditionDto> Conditions
);

using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Result of an economic or financial appraisal recalculation.
/// </summary>
public sealed record FinancialAppraisalResult(
    string MethodName,
    decimal? SubmittedValue,
    decimal? CalculatedValue,
    decimal? Difference,
    CalculationStatus Status,
    string Details
);

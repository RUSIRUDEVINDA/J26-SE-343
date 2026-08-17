using System;
using System.Collections.Generic;

namespace StateLandGovernance.LeaseFeasibility.Application.DTOs;

/// <summary>
/// Data Transfer Object representing an applicant's financial profile.
/// </summary>
public sealed record FinancialProfileDto(
    string ApplicantId,
    decimal AverageMonthlyIncome,
    decimal IncomeConsistencyScore,
    int EmploymentTenureMonths,
    string EmploymentType,
    string? EmployerOrBusinessName
);

/// <summary>
/// Data Transfer Object representing an individual factor contributing to the financial feasibility score.
/// </summary>
public sealed record FeasibilityFactorDto(
    string FactorId,
    string Category,
    int ScoreContribution,
    string Description,
    bool IsPenalty
);

/// <summary>
/// Data Transfer Object representing the final A-E financial feasibility evaluation result.
/// </summary>
public sealed record FeasibilityAssessmentDto(
    string ApplicationId,
    string ApplicantId,
    int TotalScore,
    string EligibilityGrade,
    bool RequiresManualReview,
    IReadOnlyList<FeasibilityFactorDto> ContributingFactors,
    DateTime EvaluationTimestamp
);

/// <summary>
/// Data Transfer Object for the ML-driven predictive approval estimation.
/// </summary>
public sealed record ApprovalPredictionDto(
    string ApplicationId,
    decimal SuccessProbability,
    string RiskCategory,
    IReadOnlyList<string> KeyInfluencingVariables,
    DateTime PredictionTimestamp
);

/// <summary>
/// Data Transfer Object outlining specific terms synthesized by the generative optimization module.
/// </summary>
public sealed record ProposalTermsDto(
    decimal RecommendedAreaHectares,
    int RecommendedDurationYears,
    decimal EstimatedMonthlyRent,
    IReadOnlyList<string> SpecialConditions
);

/// <summary>
/// Data Transfer Object representing an AI-generated, optimized lease proposal based on historical success patterns (RAG).
/// </summary>
public sealed record LeaseProposalDto(
    string ProposalId,
    string ApplicationId,
    ProposalTermsDto ProposedTerms,
    decimal OptimizationConfidence,
    IReadOnlyList<string> ReferencedHistoricalCaseIds,
    DateTime GeneratedTimestamp
);

/// <summary>
/// Data Transfer Object carrying context from peer modules without requiring direct project references.
/// </summary>
public sealed record LeaseFeasibilityContextInputDto(
    string ApplicationId,
    string LandCategory,
    string LandUseType,
    decimal LandSuitabilityScore,
    int GovernanceRiskScore,
    string GovernanceSeverity,
    bool HasActiveComplianceViolations
);

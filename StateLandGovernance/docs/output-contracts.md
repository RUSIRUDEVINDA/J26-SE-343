# Output Contracts: Feasibility & Generative Proposal DTOs

This document outlines the proposed Data Transfer Object (DTO) shapes for Component 2 (AI-Driven Lease Feasibility Assessment and Generative Proposal Optimization Framework). 

These definitions strictly follow the immutability (`sealed record`) and collection (`IReadOnlyList<T>`) conventions established in `GovernanceRiskDtos.cs`.

```csharp
using System;
using System.Collections.Generic;

namespace StateLandGovernance.LeaseFeasibility.Application.DTOs;

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
    string EligibilityGrade, // A, B, C, D, or E
    bool RequiresManualReview,
    IReadOnlyList<FeasibilityFactorDto> ContributingFactors,
    DateTime EvaluationTimestamp
);

/// <summary>
/// Data Transfer Object for the ML-driven predictive approval estimation.
/// </summary>
public sealed record ApprovalPredictionDto(
    string ApplicationId,
    decimal SuccessProbability, // e.g., 0.85 for 85%
    string RiskCategory, // Low, Moderate, High
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
/// Data Transfer Object carrying context from peer modules (LandIntelligence and GovernanceIntelligence) 
/// without requiring direct project references, adhering to module isolation constraints.
/// </summary>
public sealed record LeaseFeasibilityContextInputDto(
    string ApplicationId,
    string LandCategory,      // From LandIntelligence: Parcel context (e.g., Crown Land, Reserved Land)
    string LandUseType,       // From LandIntelligence: (e.g., Agricultural, Commercial)
    decimal LandSuitabilityScore, // From LandIntelligence: Suitability analysis result
    int GovernanceRiskScore,  // From GovernanceIntelligence: The assessed risk score
    string GovernanceSeverity,// From GovernanceIntelligence: The risk severity level
    bool HasActiveComplianceViolations // From GovernanceIntelligence: Flag restricting feasibility
);
```

using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public sealed record GovernanceDashboardSummaryDto(
    int TotalCases,
    int TotalAssessments,
    int ClearCount,
    int ReviewRequiredCount,
    int InsufficientInfoCount,
    string LastUpdated);

public sealed record ComplaintAdvisoryPredictionDto(
    bool HasComplaint,
    string? PredictedCategory,
    double? ModelProbability,
    string ReviewStatus,
    string? ModelVersion);

public sealed record GovernanceCaseAssessmentDto(
    string AssessmentId,
    string? WorkflowRunId,
    string CaseId,
    string ParcelReference,
    string ApplicantReference,
    string OverallStatus,
    string ComplianceStatus,
    string ConflictStatus,
    string EarlyScreeningStatus,
    string AnomalyStatus,
    string ConsensusOutcome,
    string EvaluatedAt,
    string Version,
    string SummaryFinding,
    string Rationale,
    string RecommendedNextStep,
    ComplaintAdvisoryPredictionDto? ComplaintAdvisory);

public sealed record GovernanceDashboardDto(
    GovernanceDashboardSummaryDto Summary,
    IReadOnlyList<GovernanceCaseAssessmentDto> Cases);


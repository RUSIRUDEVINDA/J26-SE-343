using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

public sealed record ProjectLocationInput(
    string? Province,
    string? District,
    string? DivisionalSecretariatDivision,
    string? GramaNiladhariDivision,
    string? Description
);

public sealed record LandRequirementInput(
    bool? RequiresLand = null,
    decimal? ExtentHectares = null,
    string? LandType = null,
    string? AllocationDetails = null,
    bool? ResettlementApplicable = null
);

public sealed record PreliminaryAssessmentInput(
    bool? PreFeasibilityDone,
    string? PreFeasibilityRef,
    bool? EiaDone,
    string? EiaRef,
    IReadOnlyList<string>? AssessmentReportReferences
);

public sealed record PolicyAlignmentInput(
    IReadOnlyList<string>? DeclaredPolicies,
    string? AlignmentExplanation
);

public sealed record StakeholderInput(
    IReadOnlyList<string>? ExpectedStakeholders,
    IReadOnlyList<string>? ConsultedStakeholders,
    bool? DuplicationAssessmentSupplied,
    string? OverlapMitigationRef
);

public sealed record ResultFrameworkNodeInput(
    string? NodeId,
    string? NodeType, // Activity, Output, Outcome, Impact
    string? Title,
    string? ParentNodeId,
    IReadOnlyList<string>? KpiReferences
);

public sealed record ResultFrameworkInput(
    IReadOnlyList<ResultFrameworkNodeInput>? Nodes
);

public sealed record NegativeImpactItemInput(
    string? ImpactId,
    string? Description,
    string? MitigationPlanRef
);

public sealed record ImpactAssessmentInput(
    bool? ImpactAssessmentApplicable,
    IReadOnlyList<NegativeImpactItemInput>? NegativeImpacts
);

public sealed record RiskItemInput(
    string? RiskId,
    string? Description,
    string? MitigationStrategy,
    bool? IsAssumption = null
);

public sealed record RiskFrameworkInput(
    IReadOnlyList<RiskItemInput>? Risks
);

public sealed record KpiInput(
    string? KpiId,
    string? OutputOrOutcomeRef,
    string? UnitOfMeasure,
    decimal? BaselineValue,
    int? BaselineYear,
    decimal? TargetValue,
    string? MeansOfVerification,
    string? DataSource,
    string? ResponsibleRole
);

public sealed record MonitoringPlanInput(
    IReadOnlyList<KpiInput>? Kpis
);

public sealed record CostComponentInput(
    string? ComponentId = null,
    string? Name = null,
    decimal? Amount = null
);

public sealed record BudgetInput(
    decimal? SubmittedProjectBudget = null,
    IReadOnlyList<CostComponentInput>? CostComponents = null
);

public sealed record FinancingSourceInput(
    string? SourceId = null,
    string? Name = null,
    decimal? Amount = null
);

public sealed record FinancingInput(
    IReadOnlyList<FinancingSourceInput>? FinancingSources = null,
    bool? RevenueExpected = null,
    decimal? RevenueForecastAmount = null
);

public sealed record SocialSafeguardInput(
    bool? ResettlementApplicable,
    decimal? ResettlementCost,
    bool? GenderConsidered,
    bool? AccessibilityConsidered
);

public sealed record ImplementationActivityInput(
    string? ActivityId,
    string? Name,
    string? ResponsibleRole
);

public sealed record ImplementationInput(
    IReadOnlyList<ImplementationActivityInput>? Activities,
    string? OAndMArrangement,
    decimal? OAndMCost,
    string? OAndMFundingSource
);

public sealed record EconomicAppraisalInput(
    string? SelectedMethod,
    decimal? DiscountRate,
    IReadOnlyList<decimal>? CashFlows,
    CashFlowPeriod CashFlowPeriod,
    decimal? SubmittedNpv,
    decimal? SubmittedIrr,
    decimal? SubmittedPaybackPeriod,
    decimal? SubmittedCbr,
    decimal? SubmittedPvBenefits,
    decimal? SubmittedPvCosts,
    CostBenefitRatioConvention CbrConvention
);

public sealed record EvidenceReferenceInput(
    string? EvidenceId,
    string? ReferenceNumber,
    EvidenceStatus VerificationStatus
);

public sealed record DisasterRiskAssessmentInput(
    bool? Applicable,
    bool? AssessmentCompleted,
    string? AssessmentReference,
    IReadOnlyList<string>? HazardsConsidered,
    IReadOnlyList<string>? MitigationMeasures,
    string? ResponsibleRole,
    string? Remarks
);

/// <summary>
/// Domain input value object containing structured facts for NPD compliance evaluation.
/// </summary>
public sealed record ProposalComplianceInput(
    string ProposalId,
    ProjectLocationInput? Location = null,
    LandRequirementInput? LandRequirement = null,
    PreliminaryAssessmentInput? PreliminaryAssessment = null,
    PolicyAlignmentInput? PolicyAlignment = null,
    StakeholderInput? Stakeholders = null,
    ResultFrameworkInput? ResultFramework = null,
    ImpactAssessmentInput? ImpactAssessment = null,
    RiskFrameworkInput? RiskFramework = null,
    DisasterRiskAssessmentInput? DisasterRiskAssessment = null,
    MonitoringPlanInput? MonitoringPlan = null,
    BudgetInput? Budget = null,
    FinancingInput? Financing = null,
    SocialSafeguardInput? SocialSafeguard = null,
    ImplementationInput? Implementation = null,
    EconomicAppraisalInput? EconomicAppraisal = null,
    IReadOnlyList<EvidenceReferenceInput>? EvidenceReferences = null
);

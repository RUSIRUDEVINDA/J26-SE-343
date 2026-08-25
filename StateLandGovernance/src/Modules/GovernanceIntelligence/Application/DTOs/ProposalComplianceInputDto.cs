using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public sealed record ProjectLocationDto(
    string Province,
    string District,
    string DivisionalSecretariatDivision,
    string GramaNiladhariDivision,
    string Description
);

public sealed record LandRequirementDto(
    bool RequiresLand,
    decimal? ExtentHectares,
    string LandType,
    string AllocationDetails,
    bool? ResettlementApplicable
);

public sealed record PreliminaryAssessmentDto(
    bool? PreFeasibilityDone,
    string PreFeasibilityRef,
    bool? EiaDone,
    string EiaRef,
    IReadOnlyList<string> AssessmentReportReferences
);

public sealed record PolicyAlignmentDto(
    IReadOnlyList<string> DeclaredPolicies,
    string AlignmentExplanation
);

public sealed record StakeholderDto(
    IReadOnlyList<string> ExpectedStakeholders,
    IReadOnlyList<string> ConsultedStakeholders,
    bool? DuplicationAssessmentSupplied,
    string OverlapMitigationRef
);

public sealed record ResultFrameworkNodeDto(
    string NodeId,
    string NodeType,
    string Title,
    string ParentNodeId,
    IReadOnlyList<string> KpiReferences
);

public sealed record ResultFrameworkDto(
    IReadOnlyList<ResultFrameworkNodeDto> Nodes
);

public sealed record NegativeImpactItemDto(
    string ImpactId,
    string Description,
    string MitigationPlanRef
);

public sealed record ImpactAssessmentDto(
    bool? ImpactAssessmentApplicable,
    IReadOnlyList<NegativeImpactItemDto> NegativeImpacts
);

public sealed record RiskItemDto(
    string RiskId,
    string Description,
    string MitigationStrategy,
    bool IsAssumption
);

public sealed record RiskFrameworkDto(
    IReadOnlyList<RiskItemDto> Risks
);

public sealed record KpiDto(
    string KpiId,
    string OutputOrOutcomeRef,
    string UnitOfMeasure,
    decimal? BaselineValue,
    int? BaselineYear,
    decimal? TargetValue,
    string MeansOfVerification,
    string DataSource,
    string ResponsibleRole
);

public sealed record MonitoringPlanDto(
    IReadOnlyList<KpiDto> Kpis
);

public sealed record CostComponentDto(
    string ComponentId,
    string Name,
    decimal Amount
);

public sealed record BudgetDto(
    decimal SubmittedProjectBudget,
    IReadOnlyList<CostComponentDto> CostComponents
);

public sealed record FinancingSourceDto(
    string SourceId,
    string Name,
    decimal Amount
);

public sealed record FinancingDto(
    IReadOnlyList<FinancingSourceDto> FinancingSources,
    bool RevenueExpected,
    decimal? RevenueForecastAmount
);

public sealed record SocialSafeguardDto(
    bool? ResettlementApplicable,
    decimal? ResettlementCost,
    bool? GenderConsidered,
    bool? AccessibilityConsidered
);

public sealed record ImplementationActivityDto(
    string ActivityId,
    string Name,
    string ResponsibleRole
);

public sealed record ImplementationDto(
    IReadOnlyList<ImplementationActivityDto> Activities,
    string OAndMArrangement,
    decimal? OAndMCost,
    string OAndMFundingSource
);

public sealed record EconomicAppraisalDto(
    string SelectedMethod,
    decimal? DiscountRate,
    IReadOnlyList<decimal> CashFlows,
    string CashFlowPeriod, // Annual, Quarterly, Monthly
    decimal? SubmittedNpv,
    decimal? SubmittedIrr,
    decimal? SubmittedPaybackPeriod,
    decimal? SubmittedCbr,
    decimal? SubmittedPvBenefits,
    decimal? SubmittedPvCosts,
    string CbrConvention // BenefitsOverCosts, CostsOverBenefits, Unresolved
);

public sealed record EvidenceReferenceDto(
    string EvidenceId,
    string ReferenceNumber,
    string VerificationStatus
);

/// <summary>
/// DTO containing structured proposal facts for NPD compliance evaluation requests.
/// </summary>
public sealed record ProposalComplianceInputDto(
    string ProposalId,
    ProjectLocationDto Location,
    LandRequirementDto LandRequirement,
    PreliminaryAssessmentDto PreliminaryAssessment,
    PolicyAlignmentDto PolicyAlignment,
    StakeholderDto Stakeholders,
    ResultFrameworkDto ResultFramework,
    ImpactAssessmentDto ImpactAssessment,
    RiskFrameworkDto RiskFramework,
    MonitoringPlanDto MonitoringPlan,
    BudgetDto Budget,
    FinancingDto Financing,
    SocialSafeguardDto SocialSafeguard,
    ImplementationDto Implementation,
    EconomicAppraisalDto EconomicAppraisal,
    IReadOnlyList<EvidenceReferenceDto> EvidenceReferences
);

using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public sealed record ProjectLocationDto(
    string? Province = null,
    string? District = null,
    string? DivisionalSecretariatDivision = null,
    string? GramaNiladhariDivision = null,
    string? Description = null
);

public sealed record LandRequirementDto(
    bool? RequiresLand = null,
    decimal? ExtentHectares = null,
    string? LandType = null,
    string? AllocationDetails = null,
    bool? ResettlementApplicable = null
);

public sealed record PreliminaryAssessmentDto(
    bool? PreFeasibilityDone = null,
    string? PreFeasibilityRef = null,
    bool? EiaDone = null,
    string? EiaRef = null,
    IReadOnlyList<string>? AssessmentReportReferences = null
);

public sealed record PolicyAlignmentDto(
    IReadOnlyList<string>? DeclaredPolicies = null,
    string? AlignmentExplanation = null
);

public sealed record StakeholderDto(
    IReadOnlyList<string>? ExpectedStakeholders = null,
    IReadOnlyList<string>? ConsultedStakeholders = null,
    bool? DuplicationAssessmentSupplied = null,
    string? OverlapMitigationRef = null
);

public sealed record ResultFrameworkNodeDto(
    string? NodeId = null,
    string? NodeType = null,
    string? Title = null,
    string? ParentNodeId = null,
    IReadOnlyList<string>? KpiReferences = null
);

public sealed record ResultFrameworkDto(
    IReadOnlyList<ResultFrameworkNodeDto>? Nodes = null
);

public sealed record NegativeImpactItemDto(
    string? ImpactId = null,
    string? Description = null,
    string? MitigationPlanRef = null
);

public sealed record ImpactAssessmentDto(
    bool? ImpactAssessmentApplicable = null,
    IReadOnlyList<NegativeImpactItemDto>? NegativeImpacts = null
);

public sealed record RiskItemDto(
    string? RiskId = null,
    string? Description = null,
    string? MitigationStrategy = null,
    bool? IsAssumption = null
);

public sealed record RiskFrameworkDto(
    IReadOnlyList<RiskItemDto>? Risks = null
);

public sealed record KpiDto(
    string? KpiId = null,
    string? OutputOrOutcomeRef = null,
    string? UnitOfMeasure = null,
    decimal? BaselineValue = null,
    int? BaselineYear = null,
    decimal? TargetValue = null,
    string? MeansOfVerification = null,
    string? DataSource = null,
    string? ResponsibleRole = null
);

public sealed record MonitoringPlanDto(
    IReadOnlyList<KpiDto>? Kpis = null
);

public sealed record CostComponentDto(
    string? ComponentId = null,
    string? Name = null,
    decimal? Amount = null
);

public sealed record BudgetDto(
    decimal? SubmittedProjectBudget = null,
    IReadOnlyList<CostComponentDto>? CostComponents = null
);

public sealed record FinancingSourceDto(
    string? SourceId = null,
    string? Name = null,
    decimal? Amount = null
);

public sealed record FinancingDto(
    IReadOnlyList<FinancingSourceDto>? FinancingSources = null,
    bool? RevenueExpected = null,
    decimal? RevenueForecastAmount = null
);

public sealed record SocialSafeguardDto(
    bool? ResettlementApplicable = null,
    decimal? ResettlementCost = null,
    bool? GenderConsidered = null,
    bool? AccessibilityConsidered = null
);

public sealed record ImplementationActivityDto(
    string? ActivityId = null,
    string? Name = null,
    string? ResponsibleRole = null
);

public sealed record ImplementationDto(
    IReadOnlyList<ImplementationActivityDto>? Activities = null,
    string? OAndMArrangement = null,
    decimal? OAndMCost = null,
    string? OAndMFundingSource = null
);

public sealed record EconomicAppraisalDto(
    string? SelectedMethod = null,
    decimal? DiscountRate = null,
    IReadOnlyList<decimal>? CashFlows = null,
    string? CashFlowPeriod = null, // Annual, Quarterly, Monthly
    decimal? SubmittedNpv = null,
    decimal? SubmittedIrr = null,
    decimal? SubmittedPaybackPeriod = null,
    decimal? SubmittedCbr = null,
    decimal? SubmittedPvBenefits = null,
    decimal? SubmittedPvCosts = null,
    string? CbrConvention = null // BenefitsOverCosts, CostsOverBenefits, Unresolved
);

public sealed record DisasterRiskAssessmentDto(
    bool? Applicable = null,
    bool? AssessmentCompleted = null,
    string? AssessmentReference = null,
    IReadOnlyList<string>? HazardsConsidered = null,
    IReadOnlyList<string>? MitigationMeasures = null,
    string? ResponsibleRole = null,
    string? Remarks = null
);

public sealed record EvidenceReferenceDto(
    string? EvidenceId = null,
    string? ReferenceNumber = null,
    string? VerificationStatus = null
);

/// <summary>
/// DTO containing structured proposal facts for NPD compliance evaluation requests.
/// </summary>
public sealed record ProposalComplianceInputDto(
    string ProposalId,
    ProjectLocationDto? Location = null,
    LandRequirementDto? LandRequirement = null,
    PreliminaryAssessmentDto? PreliminaryAssessment = null,
    PolicyAlignmentDto? PolicyAlignment = null,
    StakeholderDto? Stakeholders = null,
    ResultFrameworkDto? ResultFramework = null,
    ImpactAssessmentDto? ImpactAssessment = null,
    RiskFrameworkDto? RiskFramework = null,
    DisasterRiskAssessmentDto? DisasterRiskAssessment = null,
    MonitoringPlanDto? MonitoringPlan = null,
    BudgetDto? Budget = null,
    FinancingDto? Financing = null,
    SocialSafeguardDto? SocialSafeguard = null,
    ImplementationDto? Implementation = null,
    EconomicAppraisalDto? EconomicAppraisal = null,
    IReadOnlyList<EvidenceReferenceDto>? EvidenceReferences = null
);

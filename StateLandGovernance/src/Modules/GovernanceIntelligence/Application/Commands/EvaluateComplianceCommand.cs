using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Command to evaluate regulatory compliance for a proposed lease or NPD project proposal.
/// </summary>
public sealed record EvaluateComplianceCommand(
    string ActionName,
    int LeaseDurationYears = 0,
    string ProposedUse = null,
    decimal LeaseAmount = 0m,
    string ZoningArea = null,
    ProposalComplianceInputDto Input = null
);

/// <summary>
/// Handler for the EvaluateComplianceCommand.
/// </summary>
public sealed class EvaluateComplianceCommandHandler
{
    private readonly IRegulatoryRuleProvider _ruleProvider;
    private readonly IRegulatoryComplianceEngine _complianceEngine;
    private readonly IGovernanceEvaluationStore _evaluationStore;
    private readonly TimeProvider _timeProvider;

    public EvaluateComplianceCommandHandler(
        IRegulatoryRuleProvider ruleProvider,
        IRegulatoryComplianceEngine complianceEngine,
        IGovernanceEvaluationStore evaluationStore,
        TimeProvider timeProvider = null)
    {
        _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _evaluationStore = evaluationStore ?? throw new ArgumentNullException(nameof(evaluationStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ComplianceResultDto> HandleAsync(EvaluateComplianceCommand command, CancellationToken cancellationToken = default)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        DateTime utcTimestamp = _timeProvider.GetUtcNow().UtcDateTime;
        ComplianceResult result;

        if (command.Input != null)
        {
            var domainInput = MapDtoToDomainInput(command.Input);
            result = _complianceEngine.EvaluateNpd(domainInput, utcTimestamp);
        }
        else
        {
            // Legacy evaluation fallback
            var rules = await _ruleProvider.GetActiveRulesAsync(cancellationToken);
            var legacyInput = new LeaseEvaluationInput(
                command.LeaseDurationYears,
                command.ProposedUse,
                command.LeaseAmount,
                command.ZoningArea);

            result = _complianceEngine.Evaluate(legacyInput, rules);
        }

        // Store audit record & evaluation output atomically
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            command.ActionName ?? "EvaluateCompliance",
            result.Status.ToString(),
            $"Findings: {result.Findings.Count}, Violations: {result.Violations.Count}, Conditions: {result.Conditions.Count}",
            utcTimestamp);

        await _evaluationStore.StoreComplianceEvaluationAsync(auditRecord, result, command.ActionName ?? "EvaluateCompliance", cancellationToken);

        // Map to result DTO
        var violationDtos = result.Violations
            .Select(v => new ViolationDto(v.RuleCode, v.Message))
            .ToList();

        var conditionDtos = result.Conditions
            .Select(c => new ComplianceConditionDto(c.Description, c.RequiredByDate))
            .ToList();

        var findingDtos = result.Findings
            .Select(f => new ComplianceFindingDto(
                f.RuleCode,
                f.RuleVersion,
                f.Category.ToString(),
                f.Applicability.ToString(),
                f.Status.ToString(),
                f.Severity,
                f.IsBlocking,
                f.RequiresHumanReview,
                f.ObservedValueSummary,
                f.ExpectedRequirement,
                f.EvidenceStatus.ToString(),
                f.CalculationStatus.ToString(),
                f.SourceReference?.SourceAuthority ?? string.Empty,
                f.SourceReference?.SourceDocument ?? string.Empty,
                f.SourceReference?.SourceSection ?? string.Empty,
                f.RecommendedAction
            ))
            .ToList();

        return new ComplianceResultDto(
            result.Status.ToString(),
            violationDtos,
            conditionDtos,
            findingDtos,
            result.DeterministicEvaluationId
        );
    }

    private static ComplianceStatus CombineStatus(ComplianceStatus legacyStatus, ComplianceStatus npdStatus)
    {
        if (legacyStatus == ComplianceStatus.NonCompliant || npdStatus == ComplianceStatus.NonCompliant)
            return ComplianceStatus.NonCompliant;
        if (npdStatus == ComplianceStatus.InsufficientInformation)
            return ComplianceStatus.InsufficientInformation;
        if (legacyStatus == ComplianceStatus.Conditional || npdStatus == ComplianceStatus.Conditional)
            return ComplianceStatus.Conditional;
        if (npdStatus == ComplianceStatus.RequiresHumanReview)
            return ComplianceStatus.RequiresHumanReview;
        return ComplianceStatus.Compliant;
    }

    private static ProposalComplianceInput MapLegacyToProposalInput(EvaluateComplianceCommand cmd)
    {
        return new ProposalComplianceInput(
            ProposalId: "LEGACY-PROP-001",
            Location: new ProjectLocationInput("Western", "Colombo", "Colombo DSD", "GND-101", cmd.ZoningArea ?? "Zoning Area"),
            LandRequirement: new LandRequirementInput(true, 1.0m, cmd.ProposedUse ?? "Commercial", $"Legacy Lease {cmd.LeaseDurationYears} Years", false),
            PreliminaryAssessment: new PreliminaryAssessmentInput(true, "PRE-FEAS-LEGACY", true, "EIA-LEGACY", new[] { "DOC-LEGACY-01" }),
            PolicyAlignment: new PolicyAlignmentInput(new[] { "National Land Policy" }, "Legacy Zoning Match"),
            Stakeholders: new StakeholderInput(new[] { "LRA", "DS" }, new[] { "LRA", "DS" }, true, "N/A"),
            ResultFramework: new ResultFrameworkInput(new[] { new ResultFrameworkNodeInput("N-1", "Impact", "Development", "", new[] { "KPI-1" }) }),
            ImpactAssessment: new ImpactAssessmentInput(false, Array.Empty<NegativeImpactItemInput>()),
            RiskFramework: new RiskFrameworkInput(new[] { new RiskItemInput("R-1", "Operational Risk", "Mitigated", false) }),
            MonitoringPlan: new MonitoringPlanInput(new[] { new KpiInput("KPI-1", "N-1", "Units", 0, 2024, 100, "Inspection", "Registry", "Officer") }),
            Budget: new BudgetInput(cmd.LeaseAmount, new[] { new CostComponentInput("C-1", "Lease Fee", cmd.LeaseAmount) }),
            Financing: new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", cmd.LeaseAmount) }, false, null),
            SocialSafeguard: new SocialSafeguardInput(false, null, true, true),
            Implementation: new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Lease Execution", "Land Officer") }, "Standard", 0m, "Internal"),
            EconomicAppraisal: new EconomicAppraisalInput("NPV", 0.10m, new[] { -cmd.LeaseAmount, cmd.LeaseAmount * 0.2m, cmd.LeaseAmount * 0.3m }, CashFlowPeriod.Annual, null, null, null, null, null, null, CostBenefitRatioConvention.BenefitsOverCosts),
            EvidenceReferences: new[] { new EvidenceReferenceInput("EV-1", "REF-001", EvidenceStatus.Verified) }
        );
    }

    private static ProposalComplianceInput MapDtoToDomainInput(ProposalComplianceInputDto dto)
    {
        CashFlowPeriod period = CashFlowPeriod.Annual;
        if (!string.IsNullOrWhiteSpace(dto.EconomicAppraisal?.CashFlowPeriod) &&
            Enum.TryParse<CashFlowPeriod>(dto.EconomicAppraisal.CashFlowPeriod, true, out var parsedPeriod))
        {
            period = parsedPeriod;
        }

        CostBenefitRatioConvention cbrConvention = CostBenefitRatioConvention.Unresolved;
        if (!string.IsNullOrWhiteSpace(dto.EconomicAppraisal?.CbrConvention) &&
            Enum.TryParse<CostBenefitRatioConvention>(dto.EconomicAppraisal.CbrConvention, true, out var parsedCbr))
        {
            cbrConvention = parsedCbr;
        }

        return new ProposalComplianceInput(
            dto.ProposalId ?? "PROP-NPD",
            dto.Location != null ? new ProjectLocationInput(dto.Location.Province, dto.Location.District, dto.Location.DivisionalSecretariatDivision, dto.Location.GramaNiladhariDivision, dto.Location.Description) : null,
            dto.LandRequirement != null ? new LandRequirementInput(dto.LandRequirement.RequiresLand, dto.LandRequirement.ExtentHectares, dto.LandRequirement.LandType, dto.LandRequirement.AllocationDetails, dto.LandRequirement.ResettlementApplicable) : null,
            dto.PreliminaryAssessment != null ? new PreliminaryAssessmentInput(dto.PreliminaryAssessment.PreFeasibilityDone, dto.PreliminaryAssessment.PreFeasibilityRef, dto.PreliminaryAssessment.EiaDone, dto.PreliminaryAssessment.EiaRef, dto.PreliminaryAssessment.AssessmentReportReferences) : null,
            dto.PolicyAlignment != null ? new PolicyAlignmentInput(dto.PolicyAlignment.DeclaredPolicies, dto.PolicyAlignment.AlignmentExplanation) : null,
            dto.Stakeholders != null ? new StakeholderInput(dto.Stakeholders.ExpectedStakeholders, dto.Stakeholders.ConsultedStakeholders, dto.Stakeholders.DuplicationAssessmentSupplied, dto.Stakeholders.OverlapMitigationRef) : null,
            dto.ResultFramework != null && dto.ResultFramework.Nodes != null ? new ResultFrameworkInput(dto.ResultFramework.Nodes.Select(n => new ResultFrameworkNodeInput(n.NodeId, n.NodeType, n.Title, n.ParentNodeId, n.KpiReferences)).ToList()) : null,
            dto.ImpactAssessment != null ? new ImpactAssessmentInput(dto.ImpactAssessment.ImpactAssessmentApplicable, dto.ImpactAssessment.NegativeImpacts?.Select(i => new NegativeImpactItemInput(i.ImpactId, i.Description, i.MitigationPlanRef)).ToList()) : null,
            dto.RiskFramework != null && dto.RiskFramework.Risks != null ? new RiskFrameworkInput(dto.RiskFramework.Risks.Select(r => new RiskItemInput(r.RiskId, r.Description, r.MitigationStrategy, r.IsAssumption)).ToList()) : null,
            dto.MonitoringPlan != null && dto.MonitoringPlan.Kpis != null ? new MonitoringPlanInput(dto.MonitoringPlan.Kpis.Select(k => new KpiInput(k.KpiId, k.OutputOrOutcomeRef, k.UnitOfMeasure, k.BaselineValue, k.BaselineYear, k.TargetValue, k.MeansOfVerification, k.DataSource, k.ResponsibleRole)).ToList()) : null,
            dto.Budget != null ? new BudgetInput(dto.Budget.SubmittedProjectBudget, dto.Budget.CostComponents?.Select(c => new CostComponentInput(c.ComponentId, c.Name, c.Amount)).ToList()) : null,
            dto.Financing != null ? new FinancingInput(dto.Financing.FinancingSources?.Select(f => new FinancingSourceInput(f.SourceId, f.Name, f.Amount)).ToList(), dto.Financing.RevenueExpected, dto.Financing.RevenueForecastAmount) : null,
            dto.SocialSafeguard != null ? new SocialSafeguardInput(dto.SocialSafeguard.ResettlementApplicable, dto.SocialSafeguard.ResettlementCost, dto.SocialSafeguard.GenderConsidered, dto.SocialSafeguard.AccessibilityConsidered) : null,
            dto.Implementation != null ? new ImplementationInput(dto.Implementation.Activities?.Select(a => new ImplementationActivityInput(a.ActivityId, a.Name, a.ResponsibleRole)).ToList(), dto.Implementation.OAndMArrangement, dto.Implementation.OAndMCost, dto.Implementation.OAndMFundingSource) : null,
            dto.EconomicAppraisal != null ? new EconomicAppraisalInput(dto.EconomicAppraisal.SelectedMethod, dto.EconomicAppraisal.DiscountRate, dto.EconomicAppraisal.CashFlows, period, dto.EconomicAppraisal.SubmittedNpv, dto.EconomicAppraisal.SubmittedIrr, dto.EconomicAppraisal.SubmittedPaybackPeriod, dto.EconomicAppraisal.SubmittedCbr, dto.EconomicAppraisal.SubmittedPvBenefits, dto.EconomicAppraisal.SubmittedPvCosts, cbrConvention) : null,
            dto.EvidenceReferences != null ? dto.EvidenceReferences.Select(e => new EvidenceReferenceInput(e.EvidenceId, e.ReferenceNumber, Enum.TryParse<EvidenceStatus>(e.VerificationStatus, true, out var status) ? status : EvidenceStatus.Provided)).ToList() : null
        );
    }
}

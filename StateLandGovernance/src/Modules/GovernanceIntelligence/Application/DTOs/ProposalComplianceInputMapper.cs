using System;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;
public static class ProposalComplianceInputMapper
{
    public static ProposalComplianceInput Map(ProposalComplianceInputDto dto, bool strictDemonstrationInput = false)
    {
        if (strictDemonstrationInput && dto.EconomicAppraisal?.CashFlows?.Count > 0 &&
            (!Enum.TryParse<CashFlowPeriod>(dto.EconomicAppraisal.CashFlowPeriod,true,out var suppliedPeriod) || !Enum.IsDefined(suppliedPeriod)))
            throw new InvalidOperationException("Cash flows require an explicit supported period; the demonstration must not assume Annual.");
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
            dto.ProposalId ?? string.Empty,
            dto.Location != null ? new ProjectLocationInput(dto.Location.Province, dto.Location.District, dto.Location.DivisionalSecretariatDivision, dto.Location.GramaNiladhariDivision, dto.Location.Description) : null,
            dto.LandRequirement != null ? new LandRequirementInput(dto.LandRequirement.RequiresLand, dto.LandRequirement.ExtentHectares, dto.LandRequirement.LandType, dto.LandRequirement.AllocationDetails, dto.LandRequirement.ResettlementApplicable) : null,
            dto.PreliminaryAssessment != null ? new PreliminaryAssessmentInput(dto.PreliminaryAssessment.PreFeasibilityDone, dto.PreliminaryAssessment.PreFeasibilityRef, dto.PreliminaryAssessment.EiaDone, dto.PreliminaryAssessment.EiaRef, dto.PreliminaryAssessment.AssessmentReportReferences) : null,
            dto.PolicyAlignment != null ? new PolicyAlignmentInput(dto.PolicyAlignment.DeclaredPolicies, dto.PolicyAlignment.AlignmentExplanation) : null,
            dto.Stakeholders != null ? new StakeholderInput(dto.Stakeholders.ExpectedStakeholders, dto.Stakeholders.ConsultedStakeholders, dto.Stakeholders.DuplicationAssessmentSupplied, dto.Stakeholders.OverlapMitigationRef) : null,
            dto.ResultFramework != null && dto.ResultFramework.Nodes != null ? new ResultFrameworkInput(dto.ResultFramework.Nodes.Select(n => new ResultFrameworkNodeInput(n.NodeId, n.NodeType, n.Title, n.ParentNodeId, n.KpiReferences)).ToList()) : null,
            dto.ImpactAssessment != null ? new ImpactAssessmentInput(dto.ImpactAssessment.ImpactAssessmentApplicable, dto.ImpactAssessment.NegativeImpacts?.Select(i => new NegativeImpactItemInput(i.ImpactId, i.Description, i.MitigationPlanRef)).ToList()) : null,
            dto.RiskFramework != null && dto.RiskFramework.Risks != null ? new RiskFrameworkInput(dto.RiskFramework.Risks.Select(r => new RiskItemInput(r.RiskId, r.Description, r.MitigationStrategy, r.IsAssumption)).ToList()) : null,
            dto.DisasterRiskAssessment != null ? new DisasterRiskAssessmentInput(dto.DisasterRiskAssessment.Applicable, dto.DisasterRiskAssessment.AssessmentCompleted, dto.DisasterRiskAssessment.AssessmentReference, dto.DisasterRiskAssessment.HazardsConsidered, dto.DisasterRiskAssessment.MitigationMeasures, dto.DisasterRiskAssessment.ResponsibleRole, dto.DisasterRiskAssessment.Remarks) : null,
            dto.MonitoringPlan != null && dto.MonitoringPlan.Kpis != null ? new MonitoringPlanInput(dto.MonitoringPlan.Kpis.Select(k => new KpiInput(k.KpiId, k.OutputOrOutcomeRef, k.UnitOfMeasure, k.BaselineValue, k.BaselineYear, k.TargetValue, k.MeansOfVerification, k.DataSource, k.ResponsibleRole)).ToList()) : null,
            dto.Budget != null ? new BudgetInput(dto.Budget.SubmittedProjectBudget, dto.Budget.CostComponents?.Select(c => new CostComponentInput(c.ComponentId, c.Name, c.Amount)).ToList()) : null,
            dto.Financing != null ? new FinancingInput(dto.Financing.FinancingSources?.Select(f => new FinancingSourceInput(f.SourceId, f.Name, f.Amount)).ToList(), dto.Financing.RevenueExpected, dto.Financing.RevenueForecastAmount) : null,
            dto.SocialSafeguard != null ? new SocialSafeguardInput(dto.SocialSafeguard.ResettlementApplicable, dto.SocialSafeguard.ResettlementCost, dto.SocialSafeguard.GenderConsidered, dto.SocialSafeguard.AccessibilityConsidered) : null,
            dto.Implementation != null ? new ImplementationInput(dto.Implementation.Activities?.Select(a => new ImplementationActivityInput(a.ActivityId, a.Name, a.ResponsibleRole)).ToList(), dto.Implementation.OAndMArrangement, dto.Implementation.OAndMCost, dto.Implementation.OAndMFundingSource) : null,
            dto.EconomicAppraisal != null ? new EconomicAppraisalInput(dto.EconomicAppraisal.SelectedMethod, dto.EconomicAppraisal.DiscountRate, dto.EconomicAppraisal.CashFlows, period, dto.EconomicAppraisal.SubmittedNpv, dto.EconomicAppraisal.SubmittedIrr, dto.EconomicAppraisal.SubmittedPaybackPeriod, dto.EconomicAppraisal.SubmittedCbr, dto.EconomicAppraisal.SubmittedPvBenefits, dto.EconomicAppraisal.SubmittedPvCosts, cbrConvention) : null,
            dto.EvidenceReferences != null ? dto.EvidenceReferences.Select(e => new EvidenceReferenceInput(e.EvidenceId, e.ReferenceNumber,
                Enum.TryParse<EvidenceStatus>(e.VerificationStatus, true, out var status) && (!strictDemonstrationInput || Enum.IsDefined(status))
                    ? status : strictDemonstrationInput ? EvidenceStatus.Missing : EvidenceStatus.Provided)).ToList() : null
        );
    }
}

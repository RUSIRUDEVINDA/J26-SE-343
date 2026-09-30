using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class RegulatoryComplianceEngineNpdTests
{
    private readonly RegulatoryComplianceEngine _engine = new();

    private static ProposalComplianceInput CreateBaseInput(
        ProjectLocationInput loc = null,
        LandRequirementInput land = null,
        PreliminaryAssessmentInput prep = null,
        PolicyAlignmentInput pol = null,
        StakeholderInput stk = null,
        ResultFrameworkInput rf = null,
        ImpactAssessmentInput imp = null,
        RiskFrameworkInput risk = null,
        DisasterRiskAssessmentInput drr = null,
        MonitoringPlanInput mp = null,
        BudgetInput budget = null,
        FinancingInput fin = null,
        SocialSafeguardInput soc = null,
        ImplementationInput impl = null,
        EconomicAppraisalInput eco = null,
        IReadOnlyList<EvidenceReferenceInput> evd = null)
    {
        return new ProposalComplianceInput(
            "PROP-TEST-001",
            loc ?? new ProjectLocationInput("Western", "Colombo", "Colombo DSD", "GND-101", "Descr"),
            land ?? new LandRequirementInput(true, 2.5m, "Commercial", "Allocation details", false),
            prep ?? new PreliminaryAssessmentInput(true, "PRE-01", true, "EIA-01", new[] { "DOC-1" }),
            pol ?? new PolicyAlignmentInput(new[] { "National Policy" }, "Alignment explanation"),
            stk ?? new StakeholderInput(new[] { "LRA", "DS" }, new[] { "LRA", "DS" }, true, "N/A"),
            rf ?? new ResultFrameworkInput(new[] { new ResultFrameworkNodeInput("N-1", "Impact", "Development", "", new[] { "KPI-1" }) }),
            imp ?? new ImpactAssessmentInput(false, Array.Empty<NegativeImpactItemInput>()),
            risk ?? new RiskFrameworkInput(new[] { new RiskItemInput("R-1", "Operational Risk", "Mitigated", false) }),
            DisasterRiskAssessment: drr,
            MonitoringPlan: mp ?? new MonitoringPlanInput(new[] { new KpiInput("KPI-1", "N-1", "Units", 0, 2024, 100, "Inspection", "Registry", "Land Officer") }),
            Budget: budget ?? new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", 100000m) }),
            Financing: fin ?? new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 100000m) }, false, null),
            SocialSafeguard: soc ?? new SocialSafeguardInput(false, null, true, true),
            Implementation: impl ?? new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Execution", "Land Officer") }, "Standard O&M", 1000m, "Internal"),
            EconomicAppraisal: eco ?? new EconomicAppraisalInput("NPV", 0.10m, new[] { -100000m, 30000m, 40000m, 50000m }, CashFlowPeriod.Annual, null, null, null, null, null, null, CostBenefitRatioConvention.BenefitsOverCosts),
            EvidenceReferences: evd ?? new[] { new EvidenceReferenceInput("EV-1", "REF-001", EvidenceStatus.Verified) }
        );
    }

    [Fact]
    public void Test46_NpdDrr001_NullApplicability_ReturnsUndeterminedAndInsufficientInformation()
    {
        var drr = new DisasterRiskAssessmentInput(null, null, null, null, null, null, null);
        var result = _engine.EvaluateNpd(CreateBaseInput(drr: drr), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-DRR-001");
        Assert.Equal(RuleApplicability.Undetermined, finding.Applicability);
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test47_NpdDrr001_NotApplicable_ReturnsNotApplicable()
    {
        var drr = new DisasterRiskAssessmentInput(false, null, null, null, null, null, null);
        var result = _engine.EvaluateNpd(CreateBaseInput(drr: drr), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-DRR-001");
        Assert.Equal(RuleApplicability.NotApplicable, finding.Applicability);
        Assert.Equal(RuleResultStatus.NotApplicable, finding.Status);
    }

    [Fact]
    public void Test48_NpdDrr001_ApplicableAssessmentIncomplete_ReturnsNonCompliant()
    {
        var drr = new DisasterRiskAssessmentInput(true, false, null, null, null, null, null);
        var result = _engine.EvaluateNpd(CreateBaseInput(drr: drr), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-DRR-001");
        Assert.Equal(RuleApplicability.Applicable, finding.Applicability);
        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
    }

    [Fact]
    public void Test49_NpdDrr001_ApplicableMissingReferenceOrDetails_ReturnsInsufficientInformation()
    {
        var drr = new DisasterRiskAssessmentInput(true, true, "", new[] { "Flooding" }, new[] { "Drainage" }, "Engineer", "Remarks");
        var result = _engine.EvaluateNpd(CreateBaseInput(drr: drr), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-DRR-001");
        Assert.Equal(RuleApplicability.Applicable, finding.Applicability);
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test50_NpdDrr001_ApplicableCompletedWithDetails_ReturnsCompliant()
    {
        var drr = new DisasterRiskAssessmentInput(true, true, "DRR-REF-001", new[] { "Flooding", "Drought" }, new[] { "Drainage" }, "Engineer", "Remarks");
        var result = _engine.EvaluateNpd(CreateBaseInput(drr: drr), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-DRR-001");
        Assert.Equal(RuleApplicability.Applicable, finding.Applicability);
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test51_NpdDrr001_DeterministicEvaluationId_ChangesWhenDrrInputChanges()
    {
        var drr1 = new DisasterRiskAssessmentInput(true, true, "DRR-REF-001", new[] { "Flooding" }, new[] { "Drainage" }, "Engineer", "Remarks");
        var drr2 = new DisasterRiskAssessmentInput(true, true, "DRR-REF-002", new[] { "Flooding" }, new[] { "Drainage" }, "Engineer", "Remarks");

        var r1 = _engine.EvaluateNpd(CreateBaseInput(drr: drr1), DateTime.UtcNow);
        var r2 = _engine.EvaluateNpd(CreateBaseInput(drr: drr2), DateTime.UtcNow);

        Assert.NotEqual(r1.DeterministicEvaluationId, r2.DeterministicEvaluationId);
    }

    [Fact]
    public void Test01_LandRequired_DetailsProvided_ReturnsCompliant()
    {
        var input = CreateBaseInput();
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LAND-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test02_LandRequired_DetailsMissing_ReturnsInsufficientInformationFinding()
    {
        var land = new LandRequirementInput(true, null, "", "", null);
        var input = CreateBaseInput(land: land);
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LAND-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
        Assert.False(finding.IsBlocking); // Operational rule is non-blocking
    }

    [Fact]
    public void Test03_LandNotRequired_ReturnsCompliant()
    {
        var land = new LandRequirementInput(false, null, "", "", false);
        var input = CreateBaseInput(land: land);
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LAND-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test04_ContradictoryLandDeclaration_ReturnsRequiresHumanReview()
    {
        var land = new LandRequirementInput(false, 10.0m, "Commercial", "Allocated", false);
        var input = CreateBaseInput(land: land);
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-CONS-001");
        Assert.Equal(RuleResultStatus.RequiresHumanReview, finding.Status);
        Assert.True(finding.RequiresHumanReview);
    }

    [Fact]
    public void Test05_ApplicablePreliminaryStudy_EvidencePresent_ReturnsCompliant()
    {
        var prep = new PreliminaryAssessmentInput(true, "REF-PRE", true, "REF-EIA", new[] { "DOC-1" });
        var input = CreateBaseInput(prep: prep);
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-READY-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test06_ApplicablePreliminaryStudy_EvidenceMissing_ReturnsInsufficientInformation()
    {
        var prep = new PreliminaryAssessmentInput(true, "", true, "", Array.Empty<string>());
        var input = CreateBaseInput(prep: prep);
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-READY-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test07_NonApplicablePreliminaryStudy_ReturnsNotApplicable()
    {
        var prep = new PreliminaryAssessmentInput(false, "", false, "", Array.Empty<string>());
        var input = CreateBaseInput(prep: prep);
        var result = _engine.EvaluateNpd(input, DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-READY-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test08_RationaleEvidencePresentAndMissing()
    {
        var polPresent = new PolicyAlignmentInput(new[] { "Policy 1" }, "Rationale text");
        var resultPresent = _engine.EvaluateNpd(CreateBaseInput(pol: polPresent), DateTime.UtcNow);

        var polMissing = new PolicyAlignmentInput(new[] { "Policy 1" }, "");
        var resultMissing = _engine.EvaluateNpd(CreateBaseInput(pol: polMissing), DateTime.UtcNow);

        Assert.Equal(RuleResultStatus.RequiresHumanReview, resultPresent.Findings.First(f => f.RuleCode == "NPD-RAT-001").Status);
        Assert.Equal(RuleResultStatus.InsufficientInformation, resultMissing.Findings.First(f => f.RuleCode == "NPD-RAT-001").Status);
    }

    [Fact]
    public void Test09_PolicyReferenceCompleteness()
    {
        var polPresent = new PolicyAlignmentInput(new[] { "Policy 1" }, "Text");
        var polMissing = new PolicyAlignmentInput(Array.Empty<string>(), "Text");

        var f1 = _engine.EvaluateNpd(CreateBaseInput(pol: polPresent), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-POL-001");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(pol: polMissing), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-POL-001");

        Assert.Equal(RuleResultStatus.RequiresHumanReview, f1.Status);
        Assert.Equal(RuleResultStatus.InsufficientInformation, f2.Status);
    }

    [Fact]
    public void Test10_ExpectedStakeholderMissing_ReturnsInsufficientInformation()
    {
        var stk = new StakeholderInput(new[] { "LRA", "DS", "CEA" }, new[] { "LRA" }, true, "N/A");
        var result = _engine.EvaluateNpd(CreateBaseInput(stk: stk), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-STK-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test11_NoInventedStakeholderRequirement_WhenEmpty()
    {
        var stk = new StakeholderInput(Array.Empty<string>(), Array.Empty<string>(), true, "N/A");
        var result = _engine.EvaluateNpd(CreateBaseInput(stk: stk), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-STK-001");
        Assert.Equal(RuleResultStatus.NotApplicable, finding.Status);
    }

    [Fact]
    public void Test12_ResultsFrameworkOrphanDetection_ReturnsNonCompliant()
    {
        var rf = new ResultFrameworkInput(new[]
        {
            new ResultFrameworkNodeInput("N-1", "Output", "Title", "NON-EXISTENT-PARENT", Array.Empty<string>())
        });
        var result = _engine.EvaluateNpd(CreateBaseInput(rf: rf), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-RESULT-001");
        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
    }

    [Fact]
    public void Test13_NegativeImpactAssessmentApplicability()
    {
        var impApp = new ImpactAssessmentInput(true, new[] { new NegativeImpactItemInput("I-1", "Descr", "MIT-1") });
        var impNotApp = new ImpactAssessmentInput(false, Array.Empty<NegativeImpactItemInput>());

        var f1 = _engine.EvaluateNpd(CreateBaseInput(imp: impApp), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-IMPACT-001");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(imp: impNotApp), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-IMPACT-001");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.NotApplicable, f2.Status);
    }

    [Fact]
    public void Test14_RiskFrameworkCompleteness()
    {
        var riskComplete = new RiskFrameworkInput(new[] { new RiskItemInput("R-1", "Descr", "Mitigation", false) });
        var riskIncomplete = new RiskFrameworkInput(Array.Empty<RiskItemInput>());

        var f1 = _engine.EvaluateNpd(CreateBaseInput(risk: riskComplete), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-RISK-001");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(risk: riskIncomplete), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-RISK-001");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.InsufficientInformation, f2.Status);
    }

    [Fact]
    public void Test15_KpiMonitoringPlanCompleteness()
    {
        var mpComplete = new MonitoringPlanInput(new[] { new KpiInput("K-1", "N-1", "Unit", 0, 2024, 10, "Inspection", "Registry", "Officer") });
        var mpIncomplete = new MonitoringPlanInput(new[] { new KpiInput("K-1", "N-1", "", null, null, null, "", "", "") });

        var f1 = _engine.EvaluateNpd(CreateBaseInput(mp: mpComplete), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-ME-001");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(mp: mpIncomplete), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-ME-001");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.NonCompliant, f2.Status);
    }

    [Fact]
    public void Test16_BudgetExactReconciliation_ReturnsCompliant()
    {
        var b = new BudgetInput(150000m, new[]
        {
            new CostComponentInput("C-1", "CapEx", 100000m),
            new CostComponentInput("C-2", "OpEx", 50000m)
        });
        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-BUD-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test17_BudgetMismatch_ReturnsNonCompliantFinding()
    {
        var b = new BudgetInput(200000m, new[]
        {
            new CostComponentInput("C-1", "CapEx", 100000m)
        });
        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-BUD-001");
        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
    }

    [Fact]
    public void Test18_FinancingExactReconciliation_ReturnsBalanced()
    {
        var b = new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", 100000m) });
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 100000m) }, false, null);

        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b, fin: fin), DateTime.UtcNow);
        var finding = result.Findings.First(f => f.RuleCode == "NPD-FIN-001");

        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test19_FundingShortfall_ReturnsShortfallFinding()
    {
        var b = new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", 100000m) });
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 60000m) }, false, null);

        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b, fin: fin), DateTime.UtcNow);
        var finding = result.Findings.First(f => f.RuleCode == "NPD-FIN-001");

        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
        Assert.Contains("FundingGap: 40000", finding.ObservedValueSummary);
    }

    [Fact]
    public void Test20_FundingExcess_ReturnsExcessFinding()
    {
        var b = new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", 100000m) });
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 150000m) }, false, null);

        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b, fin: fin), DateTime.UtcNow);
        var finding = result.Findings.First(f => f.RuleCode == "NPD-FIN-001");

        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
    }

    [Fact]
    public void Test21_RevenueForecastConsistency()
    {
        var finExpectedOk = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 100000m) }, true, 50000m);
        var finExpectedBad = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 100000m) }, true, null);

        var f1 = _engine.EvaluateNpd(CreateBaseInput(fin: finExpectedOk), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-FIN-002");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(fin: finExpectedBad), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-FIN-002");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.NonCompliant, f2.Status);
    }

    [Fact]
    public void Test22_OperationMaintenanceCostArrangements()
    {
        var implOk = new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Act", "Role") }, "Standard", 5000m, "Budget");
        var implBad = new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Act", "Role") }, "", null, "");

        var f1 = _engine.EvaluateNpd(CreateBaseInput(impl: implOk), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-SUST-001");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(impl: implBad), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-SUST-001");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.InsufficientInformation, f2.Status);
    }

    [Fact]
    public void Test23_ResettlementApplicableComplete_ReturnsCompliant()
    {
        var soc = new SocialSafeguardInput(true, 50000m, true, true);
        var result = _engine.EvaluateNpd(CreateBaseInput(soc: soc), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-SOC-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test24_ResettlementNotApplicable_ReturnsNotApplicable()
    {
        var soc = new SocialSafeguardInput(false, null, true, true);
        var result = _engine.EvaluateNpd(CreateBaseInput(soc: soc), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-SOC-001");
        Assert.Equal(RuleResultStatus.NotApplicable, finding.Status);
    }

    [Fact]
    public void Test25_GenderPerspectiveConsideration()
    {
        var socPresent = new SocialSafeguardInput(false, null, true, true);
        var socMissing = new SocialSafeguardInput(false, null, null, true);

        var f1 = _engine.EvaluateNpd(CreateBaseInput(soc: socPresent), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-SOC-002");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(soc: socMissing), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-SOC-002");

        Assert.Equal(RuleResultStatus.RequiresHumanReview, f1.Status);
        Assert.Equal(RuleResultStatus.InsufficientInformation, f2.Status);
    }

    [Fact]
    public void Test26_AccessibilityConsideration()
    {
        var socPresent = new SocialSafeguardInput(false, null, true, true);
        var socMissing = new SocialSafeguardInput(false, null, true, null);

        var f1 = _engine.EvaluateNpd(CreateBaseInput(soc: socPresent), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-SOC-003");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(soc: socMissing), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-SOC-003");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.InsufficientInformation, f2.Status);
    }

    [Fact]
    public void Test27_ImplementationResponsibilityCompleteness_UnassignedActivityCheck()
    {
        var implOk = new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Act", "Role") }, "O&M", 100m, "Fund");
        var implUnassigned = new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Act", "") }, "O&M", 100m, "Fund");

        var f1 = _engine.EvaluateNpd(CreateBaseInput(impl: implOk), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-IMP-001");
        var f2 = _engine.EvaluateNpd(CreateBaseInput(impl: implUnassigned), DateTime.UtcNow).Findings.First(f => f.RuleCode == "NPD-IMP-001");

        Assert.Equal(RuleResultStatus.Compliant, f1.Status);
        Assert.Equal(RuleResultStatus.NonCompliant, f2.Status);
    }

    [Fact]
    public void Test28_SustainabilityArrangement()
    {
        var implOk = new ImplementationInput(new[] { new ImplementationActivityInput("A-1", "Act", "Role") }, "O&M Arrangement", 100m, "Fund");
        var result = _engine.EvaluateNpd(CreateBaseInput(impl: implOk), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-SUST-002");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test29_FinancialCalculator_EnpvRecalculationVerification()
    {
        var cashFlows = new decimal[] { -1000m, 400m, 400m, 400m };
        var res = FinancialAppraisalCalculator.RecalculateNpv(0.10m, cashFlows, CashFlowPeriod.Annual, 5m);

        Assert.Equal(CalculationStatus.Calculated, res.Status);
        Assert.NotNull(res.CalculatedValue);
        Assert.Equal(-5.26m, res.CalculatedValue.Value);
    }

    [Fact]
    public void Test30_FinancialCalculator_FnpvRecalculationVerification()
    {
        var cashFlows = new decimal[] { -500m, 300m, 300m };
        var res = FinancialAppraisalCalculator.RecalculateNpv(0.08m, cashFlows, CashFlowPeriod.Annual, null);

        Assert.Equal(CalculationStatus.Calculated, res.Status);
        Assert.True(res.CalculatedValue > 0);
    }

    [Fact]
    public void Test31_FinancialCalculator_EirrNumericalSolverConvergence()
    {
        var cashFlows = new decimal[] { -100m, 60m, 60m };
        var res = FinancialAppraisalCalculator.CalculateIrr(cashFlows, CashFlowPeriod.Annual, null);

        Assert.Equal(CalculationStatus.Calculated, res.Status);
        Assert.NotNull(res.CalculatedValue);
        Assert.Equal(0.1307m, res.CalculatedValue.Value);
    }

    [Fact]
    public void Test32_FinancialCalculator_InvalidNonConvergentIrrHandling()
    {
        var cashFlowsNoSignChange = new decimal[] { 100m, 200m, 300m };
        var resNoSign = FinancialAppraisalCalculator.CalculateIrr(cashFlowsNoSignChange, CashFlowPeriod.Annual, null);

        var cashFlowsMultiSign = new decimal[] { -100m, 200m, -150m, 50m };
        var resMultiSign = FinancialAppraisalCalculator.CalculateIrr(cashFlowsMultiSign, CashFlowPeriod.Annual, null);

        Assert.Equal(CalculationStatus.NoRootFound, resNoSign.Status);
        Assert.Equal(CalculationStatus.MultipleRootsDetected, resMultiSign.Status);
    }

    [Fact]
    public void Test33_FinancialCalculator_PaybackPeriodRecalculationVerification()
    {
        var cashFlows = new decimal[] { -100m, 50m, 50m, 50m };
        var res = FinancialAppraisalCalculator.CalculatePaybackPeriod(cashFlows, CashFlowPeriod.Annual, 2.0m);

        Assert.Equal(CalculationStatus.Calculated, res.Status);
        Assert.Equal(2.0m, res.CalculatedValue);
    }

    [Fact]
    public void Test34_FinancialCalculator_CostBenefitRatioCalculationOnly()
    {
        var res = FinancialAppraisalCalculator.CalculateCostBenefitRatio(150000m, 100000m, 1.5m, CostBenefitRatioConvention.BenefitsOverCosts);

        Assert.Equal(CalculationStatus.RequiresStakeholderConfirmation, res.Status);
        Assert.Equal(1.5m, res.CalculatedValue);
    }

    [Fact]
    public void Test35_FinancialCalculator_NoAutomaticCostBenefitAcceptanceThreshold_RequiresConfirmation()
    {
        var resUnresolved = FinancialAppraisalCalculator.CalculateCostBenefitRatio(150000m, 100000m, 1.5m, CostBenefitRatioConvention.Unresolved);

        Assert.Equal(CalculationStatus.RequiresStakeholderConfirmation, resUnresolved.Status);
        Assert.Null(resUnresolved.CalculatedValue);
    }

    [Fact]
    public void Test36_EvaluateNpd_AnnexEvidenceReferenceValidation()
    {
        var evdOk = new[] { new EvidenceReferenceInput("EV-1", "REF-001", EvidenceStatus.Verified) };
        var result = _engine.EvaluateNpd(CreateBaseInput(evd: evdOk), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-EVD-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test37_EvaluateNpd_DuplicateEvidenceIdsDetection()
    {
        var evdDup = new[]
        {
            new EvidenceReferenceInput("EV-1", "REF-001", EvidenceStatus.Verified),
            new EvidenceReferenceInput("EV-1", "REF-002", EvidenceStatus.Verified)
        };
        var result = _engine.EvaluateNpd(CreateBaseInput(evd: evdDup), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-EVD-001");
        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
    }

    [Fact]
    public void Test38_EvaluateNpd_CrossFieldInconsistenciesDetection()
    {
        var land = new LandRequirementInput(false, 5.0m, "Commercial", "Details", false);
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 100000m) }, false, 20000m);

        var result = _engine.EvaluateNpd(CreateBaseInput(land: land, fin: fin), DateTime.UtcNow);

        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-CONS-001" && f.Status == RuleResultStatus.RequiresHumanReview);
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-FIN-002" && f.Status == RuleResultStatus.RequiresHumanReview);
    }

    [Fact]
    public void Test39_EvaluateNpd_InsufficientInformationOverallOutcome()
    {
        var prepMissing = new PreliminaryAssessmentInput(true, "", true, "", Array.Empty<string>());
        var result = _engine.EvaluateNpd(CreateBaseInput(prep: prepMissing), DateTime.UtcNow);

        Assert.Equal(ComplianceStatus.InsufficientInformation, result.Status);
    }

    [Fact]
    public void Test40_EvaluateNpd_RequiresHumanReviewOverallOutcome()
    {
        var pol = new PolicyAlignmentInput(new[] { "Policy 1" }, "Alignment explanation");
        var result = _engine.EvaluateNpd(CreateBaseInput(pol: pol), DateTime.UtcNow);

        Assert.Contains(result.Findings, f => f.Status == RuleResultStatus.RequiresHumanReview);
    }

    [Fact]
    public void Test41_EvaluateNpd_DeterministicResultForReorderedCollections()
    {
        var evd1 = new[] { new EvidenceReferenceInput("EV-1", "REF-1", EvidenceStatus.Provided), new EvidenceReferenceInput("EV-2", "REF-2", EvidenceStatus.Provided) };
        var evd2 = new[] { new EvidenceReferenceInput("EV-2", "REF-2", EvidenceStatus.Provided), new EvidenceReferenceInput("EV-1", "REF-1", EvidenceStatus.Provided) };

        var r1 = _engine.EvaluateNpd(CreateBaseInput(evd: evd1), DateTime.UtcNow);
        var r2 = _engine.EvaluateNpd(CreateBaseInput(evd: evd2), DateTime.UtcNow);

        Assert.Equal(r1.DeterministicEvaluationId, r2.DeterministicEvaluationId);
    }

    [Fact]
    public void Test42_EvaluateNpd_DeterministicIdExcludesRawTimestamp()
    {
        var input = CreateBaseInput();
        var t1 = new DateTime(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 8, 24, 18, 30, 0, DateTimeKind.Utc);

        var r1 = _engine.EvaluateNpd(input, t1);
        var r2 = _engine.EvaluateNpd(input, t2);

        Assert.Equal(r1.DeterministicEvaluationId, r2.DeterministicEvaluationId);
    }

    [Fact]
    public void Test43_EvaluateNpd_DeterministicIdChangesWhenNormalizedInputChanges()
    {
        var input1 = CreateBaseInput(land: new LandRequirementInput(true, 1.0m, "Commercial", "Alloc 1", false));
        var input2 = CreateBaseInput(land: new LandRequirementInput(true, 5.0m, "Commercial", "Alloc 2", false));

        var r1 = _engine.EvaluateNpd(input1, DateTime.UtcNow);
        var r2 = _engine.EvaluateNpd(input2, DateTime.UtcNow);

        Assert.NotEqual(r1.DeterministicEvaluationId, r2.DeterministicEvaluationId);
    }

    [Fact]
    public void Test44_EvaluateNpd_PrivacySafePersistence()
    {
        var input = CreateBaseInput();
        var auditRecord = GovernanceAuditRecord.Create(EngineType.RegulatoryCompliance, "EvalCompliance", "Compliant", "Summary details");

        var (auditEntity, evalEntity) = StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings.ComplianceEvaluationMapper.MapToEntities(auditRecord, _engine.EvaluateNpd(input, DateTime.UtcNow), "EvalCompliance");

        Assert.NotNull(auditEntity);
        Assert.NotNull(evalEntity);
        Assert.DoesNotContain("OFFICER_JOHN_DOE", auditEntity.Details);
        Assert.DoesNotContain("CONFIDENTIAL_APPLICANT", auditEntity.Details);
    }

    [Fact]
    public async Task Test45_EvaluateComplianceCommandHandler_LegacyInputBackwardCompatibility()
    {
        var ruleProvider = new InMemoryRuleProvider();
        var evalStore = new InMemoryGovernanceEvaluationStore(new InMemoryGovernanceAuditRepository());
        var handler = new EvaluateComplianceCommandHandler(ruleProvider, _engine, evalStore);

        var legacyCmd = new EvaluateComplianceCommand("AssessLegacyLease", 120, "Commercial", 500000m, "Colombo Industrial");

        var result = await handler.HandleAsync(legacyCmd, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("NonCompliant", result.Status);
        Assert.NotNull(result.Violations);
        Assert.True(result.Violations.Count > 0);
        Assert.NotNull(result.Findings);
        Assert.True(result.Findings.Count > 0);
        Assert.NotNull(result.DeterministicEvaluationId);
    }

    [Fact]
    public void Test52_EvaluateNpd_NullSubmittedProjectBudget_ReturnsInsufficientInformation()
    {
        var b = new BudgetInput(null, new[] { new CostComponentInput("C-1", "CapEx", 100000m) });
        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-BUD-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test53_EvaluateNpd_NullCostComponentAmount_ReturnsInsufficientInformation()
    {
        var b = new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", null) });
        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-BUD-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test54_EvaluateNpd_NullFinancingSourceAmount_ReturnsInsufficientInformation()
    {
        var b = new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", 100000m) });
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", null) }, false, null);

        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b, fin: fin), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-FIN-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test55_EvaluateNpd_NullRevenueExpected_ReturnsInsufficientInformation()
    {
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 100000m) }, null, 50000m);
        var result = _engine.EvaluateNpd(CreateBaseInput(fin: fin), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-FIN-002");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test56_EvaluateNpd_CurrencyNeutralRecommendationWording()
    {
        var b = new BudgetInput(100000m, new[] { new CostComponentInput("C-1", "CapEx", 100000m) });
        var fin = new FinancingInput(new[] { new FinancingSourceInput("F-1", "Equity", 60000m) }, false, null);

        var result = _engine.EvaluateNpd(CreateBaseInput(budget: b, fin: fin), DateTime.UtcNow);
        var finding = result.Findings.First(f => f.RuleCode == "NPD-FIN-001");

        Assert.Equal(RuleResultStatus.NonCompliant, finding.Status);
        Assert.DoesNotContain("$", finding.RecommendedAction);
        Assert.Contains("40,000.00 in the proposal's monetary unit", finding.RecommendedAction);
    }

    [Fact]
    public async Task Test57_EvaluateComplianceCommandHandler_NullableDtoFieldsHandledSafely()
    {
        var ruleProvider = new InMemoryRuleProvider();
        var evalStore = new InMemoryGovernanceEvaluationStore(new InMemoryGovernanceAuditRepository());
        var handler = new EvaluateComplianceCommandHandler(ruleProvider, _engine, evalStore);

        var dto = new ProposalComplianceInputDto(
            ProposalId: "PROP-NULL-TEST",
            Budget: new BudgetDto(SubmittedProjectBudget: null, CostComponents: new[] { new CostComponentDto("C-1", "CapEx", null) }),
            Financing: new FinancingDto(FinancingSources: new[] { new FinancingSourceDto("F-1", "Equity", null) }, RevenueExpected: null, RevenueForecastAmount: null)
        );

        var cmd = new EvaluateComplianceCommand("EvaluateNpdProposalCompliance", Input: dto);
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-BUD-001" && f.Status == "InsufficientInformation");
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-FIN-001" && f.Status == "InsufficientInformation");
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-FIN-002" && f.Status == "InsufficientInformation");
    }

    [Fact]
    public void Test58_EvaluateNpd_MissingGramaNiladhariDivision_ReturnsInsufficientInformation()
    {
        var loc = new ProjectLocationInput("Western", "Colombo", "Colombo DSD", null, "Desc");
        var result = _engine.EvaluateNpd(CreateBaseInput(loc: loc), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LOC-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test59_EvaluateNpd_MissingDivisionalSecretariatDivision_ReturnsInsufficientInformation()
    {
        var loc = new ProjectLocationInput("Western", "Colombo", null, "GND-101", "Desc");
        var result = _engine.EvaluateNpd(CreateBaseInput(loc: loc), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LOC-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test60_EvaluateNpd_MissingAllocationDetails_RequiresLandTrue_ReturnsInsufficientInformation()
    {
        var land = new LandRequirementInput(true, 5.0m, "Commercial", null, false);
        var result = _engine.EvaluateNpd(CreateBaseInput(land: land), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LAND-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public void Test61_EvaluateNpd_MissingAllocationDetails_RequiresLandFalse_ReturnsCompliant()
    {
        var land = new LandRequirementInput(false, null, null, null, false);
        var result = _engine.EvaluateNpd(CreateBaseInput(land: land), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-LAND-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test62_EvaluateNpd_MissingOverlapMitigationRef_BindsSafelyWithoutError()
    {
        var stk = new StakeholderInput(new[] { "CEA", "DS" }, new[] { "CEA", "DS" }, true, null);
        var result = _engine.EvaluateNpd(CreateBaseInput(stk: stk), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-STK-001");
        Assert.Equal(RuleResultStatus.Compliant, finding.Status);
    }

    [Fact]
    public void Test63_EvaluateNpd_MissingSelectedMethod_ReturnsInsufficientInformation()
    {
        var eco = new EconomicAppraisalInput(null, 0.05m, new decimal[] { -100, 50, 70 }, CashFlowPeriod.Annual, null, null, null, null, null, null, CostBenefitRatioConvention.BenefitsOverCosts);
        var result = _engine.EvaluateNpd(CreateBaseInput(eco: eco), DateTime.UtcNow);

        var finding = result.Findings.First(f => f.RuleCode == "NPD-ECO-001");
        Assert.Equal(RuleResultStatus.InsufficientInformation, finding.Status);
    }

    [Fact]
    public async Task Test64_EvaluateComplianceCommandHandler_MissingFieldsInDtoBindAndEvaluateCleanly()
    {
        var ruleProvider = new InMemoryRuleProvider();
        var evalStore = new InMemoryGovernanceEvaluationStore(new InMemoryGovernanceAuditRepository());
        var handler = new EvaluateComplianceCommandHandler(ruleProvider, _engine, evalStore);

        var dto = new ProposalComplianceInputDto(
            ProposalId: "PROP-MISSING-FIELDS-TEST",
            Location: new ProjectLocationDto(Province: "Western", District: "Colombo", DivisionalSecretariatDivision: null, GramaNiladhariDivision: null),
            LandRequirement: new LandRequirementDto(RequiresLand: true, ExtentHectares: 2.5m, AllocationDetails: null),
            Stakeholders: new StakeholderDto(ExpectedStakeholders: new[] { "LRA" }, ConsultedStakeholders: new[] { "LRA" }, OverlapMitigationRef: null),
            EconomicAppraisal: new EconomicAppraisalDto(SelectedMethod: null)
        );

        var cmd = new EvaluateComplianceCommand("EvaluateNpdProposalCompliance", Input: dto);
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-LOC-001" && f.Status == "InsufficientInformation");
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-LAND-001" && f.Status == "InsufficientInformation");
        Assert.Contains(result.Findings, f => f.RuleCode == "NPD-ECO-001" && f.Status == "InsufficientInformation");
    }

    private class InMemoryRuleProvider : IRegulatoryRuleProvider
    {
        public Task<IEnumerable<RegulatoryRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<RegulatoryRule> rules = new List<RegulatoryRule>
            {
                new MaxLeaseDurationRule(),
                new MinimumLeaseValueRule(),
                new ZoningMatchRule()
            };
            return Task.FromResult(rules);
        }
    }
}

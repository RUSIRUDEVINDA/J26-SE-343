using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Authoritative rule catalogue implementing the 24 NPD operational compliance rules based on the Department of National Planning Operational Manual (January 2019).
/// Finding severity levels ("High", "Medium", "Low") represent research-configured priority indicators for decision support and are NOT official statutory severity classifications.
/// All 24 NPD operational rules default to IsBlocking = false unless explicitly confirmed by authoritative policy.
/// </summary>
public static class NpdRuleCatalogue
{
    private static readonly RuleSourceMetadata NpdSource = new(
        RuleSourceType.GovernmentOperationalManual,
        "Department of National Planning",
        "Operational Manual / Project Submission Format (January 2019)",
        "General Operational Guidance",
        null,
        "2019.1"
    );

    public static List<ComplianceFinding> EvaluateAll(ProposalComplianceInput input)
    {
        return EvaluateAll(input, GetDefaultRuleDefinitions());
    }

    public static List<ComplianceFinding> EvaluateAll(ProposalComplianceInput input, IEnumerable<ComplianceRuleDefinition> activeRules)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (activeRules == null) throw new ArgumentNullException(nameof(activeRules));

        var findings = new List<ComplianceFinding>();

        foreach (var ruleDef in activeRules.Where(r => r != null && r.Enabled))
        {
            ComplianceFinding finding = EvaluateSingleRule(input, ruleDef);
            findings.Add(ApplyRuleDefinitionMetadata(finding, ruleDef));
        }

        return findings;
    }

    private static ComplianceFinding EvaluateSingleRule(ProposalComplianceInput input, ComplianceRuleDefinition ruleDef)
    {
        return (ruleDef.CalculationKey ?? ruleDef.RuleCode) switch
        {
            "ProjectLocationCheck" or "NPD-LOC-001" => EvaluateProjectLocation(input),
            "LandRequirementCheck" or "NPD-LAND-001" => EvaluateLandRequirement(input),
            "ProjectReadinessCheck" or "NPD-READY-001" => EvaluateProjectReadiness(input),
            "CrossFieldCoherence" or "NPD-CONS-001" => EvaluateFrameworkCoherence(input),
            "ProblemRationaleCheck" or "NPD-RAT-001" => EvaluateRationaleNeed(input),
            "PolicyAlignmentCheck" or "NPD-POL-001" => EvaluatePolicyAlignment(input),
            "StakeholderConsultation" or "NPD-STK-001" => EvaluateStakeholderCoordination(input),
            "DuplicationAssessment" or "NPD-STK-002" => EvaluateDuplicationConsideration(input),
            "ResultsFrameworkLogic" or "NPD-RESULT-001" => EvaluateResultsFramework(input),
            "NegativeImpactCheck" or "NPD-IMPACT-001" => EvaluateNegativeImpactAssessment(input),
            "RiskFrameworkCheck" or "NPD-RISK-001" => EvaluateRiskFramework(input),
            "DisasterRiskReduction" or "NPD-DRR-001" => EvaluateDisasterRiskReduction(input),
            "MonitoringPlanKpis" or "NPD-ME-001" => EvaluateMonitoringEvaluationPlan(input),
            "BudgetReconciliation" or "NPD-BUD-001" => EvaluateBudgetCompleteness(input),
            "FinancingReconciliation" or "NPD-FIN-001" => EvaluateFinancingReconciliation(input),
            "RevenueForecastCheck" or "NPD-FIN-002" => EvaluateRevenueForecastConsistency(input),
            "OperationMaintenanceCost" or "NPD-SUST-001" => EvaluateOperationMaintenanceCost(input),
            "ResettlementSafeguard" or "NPD-SOC-001" => EvaluateResettlementSafeguard(input),
            "GenderPerspectiveCheck" or "NPD-SOC-002" => EvaluateGenderPerspective(input),
            "AccessibilityCheck" or "NPD-SOC-003" => EvaluateAccessibilityConsideration(input),
            "ImplementationRoles" or "NPD-IMP-001" => EvaluateImplementationArrangements(input),
            "SustainabilityArrangements" or "NPD-SUST-002" => EvaluateSustainability(input),
            "EconomicAppraisal" or "NPD-ECO-001" => EvaluateEconomicFinancialAppraisal(input),
            "EvidenceTraceability" or "NPD-EVD-001" => EvaluateEvidenceTraceability(input),
            _ => new ComplianceFinding(
                ruleDef.RuleCode,
                ruleDef.RuleVersion,
                ruleDef.EvaluationType,
                RuleApplicability.Applicable,
                RuleResultStatus.RequiresHumanReview,
                "High",
                IsBlocking: true,
                RequiresHumanReview: true,
                ObservedValueSummary: $"Unmapped calculation key '{ruleDef.CalculationKey}' for rule '{ruleDef.RuleCode}'.",
                ExpectedRequirement: "Rule must map to a valid C# deterministic evaluator.",
                EvidenceStatus: EvidenceStatus.Missing,
                CalculationStatus: CalculationStatus.MissingInputs,
                ruleDef.SourceReference,
                "Review rule configuration and calculation key mapping."
            )
        };
    }

    private static ComplianceFinding ApplyRuleDefinitionMetadata(ComplianceFinding finding, ComplianceRuleDefinition ruleDef)
    {
        return finding with
        {
            RuleCode = ruleDef.RuleCode,
            RuleVersion = ruleDef.RuleVersion,
            Category = ruleDef.EvaluationType,
            Severity = ruleDef.Severity,
            IsBlocking = ruleDef.IsBlocking,
            SourceReference = ruleDef.SourceReference with
            {
                SourceAuthority = ruleDef.SourceReference.SourceAuthority,
                SourceDocument = ruleDef.SourceReference.SourceDocument,
                SourceSection = !string.IsNullOrWhiteSpace(finding.SourceReference?.SourceSection)
                    ? finding.SourceReference.SourceSection
                    : ruleDef.SourceReference.SourceSection
            }
        };
    }

    public static IReadOnlyList<ComplianceRuleDefinition> GetDefaultRuleDefinitions()
    {
        var emptyParams = new Dictionary<string, string>();
        return new List<ComplianceRuleDefinition>
        {
            new("NPD-LOC-001", "1.0", "Project Location", RuleEvaluationType.Completeness, "Project location hierarchy check", "ProjectLocationCheck", "Medium", false, true, NpdSource with { SourceSection = "Item 3 — Project Location" }, emptyParams),
            new("NPD-LAND-001", "1.0", "Land Requirement", RuleEvaluationType.Completeness, "Land requirement extent & allocation check", "LandRequirementCheck", "High", false, true, NpdSource with { SourceSection = "Item 4 — Land Requirement" }, emptyParams),
            new("NPD-READY-001", "1.0", "Project Readiness", RuleEvaluationType.ProjectReadiness, "Project preliminary studies check", "ProjectReadinessCheck", "Medium", false, true, NpdSource with { SourceSection = "Item 5 — Project Preliminary Activities" }, emptyParams),
            new("NPD-CONS-001", "1.0", "Cross-Field Coherence", RuleEvaluationType.CrossFieldConsistency, "Cross-field consistency check", "CrossFieldCoherence", "High", false, true, NpdSource with { SourceSection = "Items 6–16 — General Coherence Guidance" }, emptyParams),
            new("NPD-RAT-001", "1.0", "Rationale of Project", RuleEvaluationType.Completeness, "Project problem & rationale check", "ProblemRationaleCheck", "Low", false, true, NpdSource with { SourceSection = "Item 7 — Rationale of Project" }, emptyParams),
            new("NPD-POL-001", "1.0", "Policy Alignment", RuleEvaluationType.PolicyAlignmentDeclaration, "National policy alignment check", "PolicyAlignmentCheck", "Low", false, true, NpdSource with { SourceSection = "Item 8 — Relationship to National Policies" }, emptyParams),
            new("NPD-STK-001", "1.0", "Stakeholder Coordination", RuleEvaluationType.StakeholderCoordinationEvidence, "Stakeholder consultation check", "StakeholderConsultation", "Medium", false, true, NpdSource with { SourceSection = "Item 9 — Stakeholder Coordination" }, emptyParams),
            new("NPD-STK-002", "1.0", "Overlap Consideration", RuleEvaluationType.Completeness, "Duplication & overlap check", "DuplicationAssessment", "Medium", false, true, NpdSource with { SourceSection = "Item 9 — Overlap Consideration" }, emptyParams),
            new("NPD-RESULT-001", "1.0", "Results Framework", RuleEvaluationType.ResultsFrameworkConsistency, "Results framework logic check", "ResultsFrameworkLogic", "High", false, true, NpdSource with { SourceSection = "Item 10 — Logic Model Guidance" }, emptyParams),
            new("NPD-IMPACT-001", "1.0", "Negative Impact Assessment", RuleEvaluationType.ImpactAssessment, "Negative impacts check", "NegativeImpactCheck", "Medium", false, true, NpdSource with { SourceSection = "Item 12 — Negative Impact Assessment" }, emptyParams),
            new("NPD-RISK-001", "1.0", "Risk Framework", RuleEvaluationType.RiskFramework, "Risk framework check", "RiskFrameworkCheck", "Medium", false, true, NpdSource with { SourceSection = "Item 13 — Risk and Assumptions" }, emptyParams),
            new("NPD-DRR-001", "1.0", "Disaster Risk Reduction", RuleEvaluationType.ImpactAssessment, "DRR assessment check", "DisasterRiskReduction", "Low", false, true, NpdSource with { SourceSection = "Item 14 — Disaster Risk Reduction" }, emptyParams),
            new("NPD-ME-001", "1.0", "Monitoring and Evaluation", RuleEvaluationType.MonitoringAndEvaluation, "M&E plan KPIs check", "MonitoringPlanKpis", "Medium", false, true, NpdSource with { SourceSection = "Item 15 — Monitoring and Evaluation" }, emptyParams),
            new("NPD-BUD-001", "1.0", "Project Budget", RuleEvaluationType.Budget, "Budget reconciliation check", "BudgetReconciliation", "High", false, true, NpdSource with { SourceSection = "Item 16 — Project Budget" }, emptyParams),
            new("NPD-FIN-001", "1.0", "Financing Plan", RuleEvaluationType.Financing, "Financing reconciliation check", "FinancingReconciliation", "High", false, true, NpdSource with { SourceSection = "Item 17 — Financing Plan" }, emptyParams),
            new("NPD-FIN-002", "1.0", "Revenue Forecast", RuleEvaluationType.Financing, "Revenue forecast consistency check", "RevenueForecastCheck", "Medium", false, true, NpdSource with { SourceSection = "Item 17.2 — Revenue Forecast" }, emptyParams),
            new("NPD-SUST-001", "1.0", "O&M Guidance", RuleEvaluationType.Sustainability, "Operation & maintenance cost check", "OperationMaintenanceCost", "Medium", false, true, NpdSource with { SourceSection = "Items 17.3 & 22 — O&M Guidance" }, emptyParams),
            new("NPD-SOC-001", "1.0", "Resettlement", RuleEvaluationType.SocialSafeguard, "Resettlement safeguard check", "ResettlementSafeguard", "High", false, true, NpdSource with { SourceSection = "Item 18 — Resettlement" }, emptyParams),
            new("NPD-SOC-002", "1.0", "Gender Perspective", RuleEvaluationType.SocialSafeguard, "Gender perspective check", "GenderPerspectiveCheck", "Low", false, true, NpdSource with { SourceSection = "Item 19 — Gender Perspective" }, emptyParams),
            new("NPD-SOC-003", "1.0", "Differently-Abled Persons", RuleEvaluationType.SocialSafeguard, "Accessibility check", "AccessibilityCheck", "Low", false, true, NpdSource with { SourceSection = "Item 20 — Differently-Abled Persons" }, emptyParams),
            new("NPD-IMP-001", "1.0", "Implementation Arrangements", RuleEvaluationType.ImplementationReadiness, "Implementation arrangements check", "ImplementationRoles", "Medium", false, true, NpdSource with { SourceSection = "Item 21 — Implementation Arrangements" }, emptyParams),
            new("NPD-SUST-002", "1.0", "Sustainability", RuleEvaluationType.Sustainability, "Sustainability plan check", "SustainabilityArrangements", "Medium", false, true, NpdSource with { SourceSection = "Item 22 — Sustainability" }, emptyParams),
            new("NPD-ECO-001", "1.0", "Economic / Financial Appraisal", RuleEvaluationType.EconomicFinancialAppraisal, "Economic financial appraisal recalculations", "EconomicAppraisal", "Low", false, true, NpdSource with { SourceSection = "Item 23 — Economic / Financial Appraisal" }, emptyParams),
            new("NPD-EVD-001", "1.0", "Guidance on Annexes", RuleEvaluationType.AnnexTraceability, "Evidence traceability check", "EvidenceTraceability", "Low", false, true, NpdSource with { SourceSection = "Guidance on Annexes" }, emptyParams)
        };
    }

    // 1. NPD-LOC-001
    private static ComplianceFinding EvaluateProjectLocation(ProposalComplianceInput input)
    {
        var loc = input.Location;
        bool complete = loc != null &&
                        !string.IsNullOrWhiteSpace(loc.Province) &&
                        !string.IsNullOrWhiteSpace(loc.District) &&
                        !string.IsNullOrWhiteSpace(loc.DivisionalSecretariatDivision) &&
                        !string.IsNullOrWhiteSpace(loc.GramaNiladhariDivision);

        return new ComplianceFinding(
            "NPD-LOC-001",
            "1.0",
            RuleEvaluationType.Completeness,
            RuleApplicability.Applicable,
            complete ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            complete ? "Low" : "Medium",
            IsBlocking: false,
            RequiresHumanReview: !complete,
            ObservedValueSummary: loc != null ? $"Province: {loc.Province}, District: {loc.District}, DSD: {loc.DivisionalSecretariatDivision}, GND: {loc.GramaNiladhariDivision}" : "Location missing",
            ExpectedRequirement: "Full project location hierarchy (Province, District, DSD, GND) must be specified.",
            EvidenceStatus: complete ? EvidenceStatus.Provided : EvidenceStatus.Missing,
            CalculationStatus: complete ? CalculationStatus.Calculated : CalculationStatus.MissingInputs,
            SourceReference: NpdSource with { SourceSection = "Item 3 — Project Location" },
            RecommendedAction: complete ? "No action required." : "Complete missing location hierarchy fields."
        );
    }

    // 2. NPD-LAND-001
    private static ComplianceFinding EvaluateLandRequirement(ProposalComplianceInput input)
    {
        var land = input.LandRequirement;
        if (land == null || !land.RequiresLand.HasValue)
        {
            return new ComplianceFinding(
                "NPD-LAND-001", "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "High", false, true,
                "Land requirement declaration is missing.", "Proposal must declare land requirement status.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 4 — Land Requirement" },
                "Provide land requirement declaration."
            );
        }

        if (land.RequiresLand == true)
        {
            bool detailsPresent = land.ExtentHectares.HasValue && land.ExtentHectares > 0 && !string.IsNullOrWhiteSpace(land.AllocationDetails);
            return new ComplianceFinding(
                "NPD-LAND-001", "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
                detailsPresent ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
                detailsPresent ? "Low" : "High", false, !detailsPresent,
                $"RequiresLand: true, Extent: {land.ExtentHectares} ha, Allocation: {land.AllocationDetails}",
                "Structured land extent and allocation details are required when land is required.",
                detailsPresent ? EvidenceStatus.Provided : EvidenceStatus.Missing,
                detailsPresent ? CalculationStatus.Calculated : CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 4 — Land Requirement" },
                detailsPresent ? "No action required." : "Supply structured land extent and allocation details."
            );
        }

        return new ComplianceFinding(
            "NPD-LAND-001", "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
            RuleResultStatus.Compliant, "Low", false, false,
            "RequiresLand: false (No land allocation required).",
            "N/A is acceptable when project does not require land.",
            EvidenceStatus.NotRequired, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 4 — Land Requirement" },
            "No action required."
        );
    }

    // 3. NPD-READY-001
    private static ComplianceFinding EvaluateProjectReadiness(ProposalComplianceInput input)
    {
        var prep = input.PreliminaryAssessment;
        if (prep == null)
        {
            return new ComplianceFinding(
                "NPD-READY-001", "1.0", RuleEvaluationType.ProjectReadiness, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Preliminary assessment data is absent.", "Supporting report references should be provided when preliminary studies are required.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 5 — Project Preliminary Activities" },
                "Supply preliminary assessment status and report references."
            );
        }

        bool preFeasOk = (prep.PreFeasibilityDone != true) || !string.IsNullOrWhiteSpace(prep.PreFeasibilityRef);
        bool eiaOk = (prep.EiaDone != true) || !string.IsNullOrWhiteSpace(prep.EiaRef);

        bool ready = preFeasOk && eiaOk;

        return new ComplianceFinding(
            "NPD-READY-001", "1.0", RuleEvaluationType.ProjectReadiness, RuleApplicability.Applicable,
            ready ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            ready ? "Low" : "Medium", false, !ready,
            $"PreFeasibility: {prep.PreFeasibilityDone} (Ref: {prep.PreFeasibilityRef}), EIA: {prep.EiaDone} (Ref: {prep.EiaRef})",
            "Required preliminary study reports must include valid reference numbers.",
            ready ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 5 — Project Preliminary Activities" },
            ready ? "No action required." : "Provide missing supporting report references for completed preliminary studies."
        );
    }

    // 4. NPD-CONS-001
    private static ComplianceFinding EvaluateFrameworkCoherence(ProposalComplianceInput input)
    {
        var issues = new List<string>();

        if (input.LandRequirement != null && input.LandRequirement.RequiresLand == false && input.LandRequirement.ExtentHectares.HasValue && input.LandRequirement.ExtentHectares > 0)
        {
            issues.Add("RequiresLand is false but positive LandExtent is declared.");
        }

        if (input.Financing != null && input.Financing.RevenueExpected == false && input.Financing.RevenueForecastAmount.HasValue && input.Financing.RevenueForecastAmount > 0)
        {
            issues.Add("RevenueExpected is false but positive RevenueForecastAmount is declared.");
        }

        if (input.SocialSafeguard != null && input.SocialSafeguard.ResettlementApplicable == false && input.SocialSafeguard.ResettlementCost.HasValue && input.SocialSafeguard.ResettlementCost > 0)
        {
            issues.Add("ResettlementApplicable is false but positive ResettlementCost is declared.");
        }

        if (input.Budget != null && input.Budget.SubmittedProjectBudget.HasValue &&
            input.Financing != null && input.Financing.FinancingSources != null && input.Financing.FinancingSources.Count > 0 && input.Financing.FinancingSources.All(f => f.Amount.HasValue))
        {
            decimal totalFinancing = input.Financing.FinancingSources.Sum(f => f.Amount!.Value);
            if (totalFinancing != input.Budget.SubmittedProjectBudget.Value)
            {
                issues.Add($"FinancingTotal ({totalFinancing}) differs from SubmittedProjectBudget ({input.Budget.SubmittedProjectBudget.Value}).");
            }
        }

        bool coherent = issues.Count == 0;

        return new ComplianceFinding(
            "NPD-CONS-001", "1.0", RuleEvaluationType.CrossFieldConsistency, RuleApplicability.Applicable,
            coherent ? RuleResultStatus.Compliant : RuleResultStatus.RequiresHumanReview,
            coherent ? "Low" : "High", false, !coherent,
            coherent ? "No framework contradictions detected." : string.Join("; ", issues),
            "Proposal sections must be mutually coherent and free of structured contradictions.",
            coherent ? EvidenceStatus.Provided : EvidenceStatus.PendingVerification, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Items 6–16 — General Coherence Guidance" },
            coherent ? "No action required." : "Review and reconcile identified cross-field contradictions."
        );
    }

    // 5. NPD-RAT-001
    private static ComplianceFinding EvaluateRationaleNeed(ProposalComplianceInput input)
    {
        bool hasRationale = input.PolicyAlignment != null && !string.IsNullOrWhiteSpace(input.PolicyAlignment.AlignmentExplanation);

        return new ComplianceFinding(
            "NPD-RAT-001", "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
            hasRationale ? RuleResultStatus.RequiresHumanReview : RuleResultStatus.InsufficientInformation,
            "Low", false, true,
            hasRationale ? "Rationale statement provided; qualitative evaluation required." : "Rationale statement missing.",
            "Project rationale must be based on identified problems/needs.",
            hasRationale ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 7 — Rationale of Project" },
            hasRationale ? "Human review recommended to assess substantive rationale quality." : "Provide project problem/need rationale."
        );
    }

    // 6. NPD-POL-001
    private static ComplianceFinding EvaluatePolicyAlignment(ProposalComplianceInput input)
    {
        var pol = input.PolicyAlignment;
        int count = pol?.DeclaredPolicies?.Count ?? 0;
        bool hasPolicies = pol != null && count > 0;

        return new ComplianceFinding(
            "NPD-POL-001", "1.0", RuleEvaluationType.PolicyAlignmentDeclaration, RuleApplicability.Applicable,
            hasPolicies ? RuleResultStatus.RequiresHumanReview : RuleResultStatus.InsufficientInformation,
            "Low", false, true,
            hasPolicies ? $"Declared {count} alignment policies." : "No national policy declarations found.",
            "Relevant national policies, strategies, or master plans should be identified.",
            hasPolicies ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 8 — Relationship to National Policies" },
            hasPolicies ? "Human review recommended to verify substantive policy alignment." : "Declare relevant national policy alignments."
        );
    }

    // 7. NPD-STK-001
    private static ComplianceFinding EvaluateStakeholderCoordination(ProposalComplianceInput input)
    {
        var stk = input.Stakeholders;
        if (stk == null || stk.ExpectedStakeholders == null || stk.ExpectedStakeholders.Count == 0)
        {
            return new ComplianceFinding(
                "NPD-STK-001", "1.0", RuleEvaluationType.StakeholderCoordinationEvidence, RuleApplicability.NotApplicable,
                RuleResultStatus.NotApplicable, "Low", false, false,
                "No expected stakeholder requirements defined.", "Check consulted stakeholders against expected stakeholders.",
                EvidenceStatus.NotRequired, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 9 — Stakeholder Coordination" },
                "No action required."
            );
        }

        var missing = stk.ExpectedStakeholders.Except(stk.ConsultedStakeholders ?? Array.Empty<string>()).ToList();
        bool ok = missing.Count == 0;

        return new ComplianceFinding(
            "NPD-STK-001", "1.0", RuleEvaluationType.StakeholderCoordinationEvidence, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            ok ? "Low" : "Medium", false, !ok,
            ok ? "All expected stakeholders consulted." : $"Missing consultations: {string.Join(", ", missing)}",
            "Expected stakeholders must be consulted.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 9 — Stakeholder Coordination" },
            ok ? "No action required." : "Conduct and document required stakeholder consultations."
        );
    }

    // 8. NPD-STK-002
    private static ComplianceFinding EvaluateDuplicationConsideration(ProposalComplianceInput input)
    {
        var stk = input.Stakeholders;
        bool checkedDuplication = stk != null && stk.DuplicationAssessmentSupplied == true;

        return new ComplianceFinding(
            "NPD-STK-002", "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
            checkedDuplication ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            checkedDuplication ? "Low" : "Medium", false, false,
            checkedDuplication ? "Duplication/overlap assessment supplied." : "Duplication assessment missing.",
            "Project preparation must evaluate overlap with existing interventions.",
            checkedDuplication ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 9 — Overlap Consideration" },
            checkedDuplication ? "No action required." : "Provide duplication/overlap assessment."
        );
    }

    // 9. NPD-RESULT-001
    private static ComplianceFinding EvaluateResultsFramework(ProposalComplianceInput input)
    {
        var rf = input.ResultFramework;
        if (rf == null || rf.Nodes == null || rf.Nodes.Count == 0)
        {
            return new ComplianceFinding(
                "NPD-RESULT-001", "1.0", RuleEvaluationType.ResultsFrameworkConsistency, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Results framework nodes missing.", "Results framework must define outputs, outcomes, and impacts.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 10 — Logic Model Guidance" },
                "Provide structured results framework."
            );
        }

        var validNodes = rf.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.NodeId)).ToList();
        var duplicateIds = validNodes.GroupBy(n => n.NodeId!).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var nodeMap = validNodes.ToDictionary(n => n.NodeId!, n => n);

        var orphans = rf.Nodes.Where(n => !string.Equals(n.NodeType, "Impact", StringComparison.OrdinalIgnoreCase) &&
                                          (string.IsNullOrWhiteSpace(n.ParentNodeId) || !nodeMap.ContainsKey(n.ParentNodeId!))).ToList();

        bool ok = duplicateIds.Count == 0 && orphans.Count == 0;

        return new ComplianceFinding(
            "NPD-RESULT-001", "1.0", RuleEvaluationType.ResultsFrameworkConsistency, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.NonCompliant,
            ok ? "Low" : "High", false, !ok,
            ok ? "Results framework logic hierarchy coherent." : $"Duplicates: {duplicateIds.Count}, Orphan nodes: {orphans.Count}",
            "Results framework nodes must form a coherent hierarchy without duplicates or orphans.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Rejected, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 10 — Logic Model Guidance" },
            ok ? "No action required." : "Fix duplicate node IDs and parent relationships in results framework."
        );
    }

    // 10. NPD-IMPACT-001
    private static ComplianceFinding EvaluateNegativeImpactAssessment(ProposalComplianceInput input)
    {
        var imp = input.ImpactAssessment;
        if (imp == null || imp.ImpactAssessmentApplicable == null)
        {
            return new ComplianceFinding(
                "NPD-IMPACT-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.Undetermined,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Negative impact assessment applicability is undetermined.", "Declare whether negative impact assessment applies.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 12 — Negative Impact Assessment" },
                "Specify negative impact assessment applicability."
            );
        }

        if (imp.ImpactAssessmentApplicable == false)
        {
            return new ComplianceFinding(
                "NPD-IMPACT-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.NotApplicable,
                RuleResultStatus.NotApplicable, "Low", false, false,
                "Negative impact assessment declared not applicable.", "N/A acceptable when no negative impacts exist.",
                EvidenceStatus.NotRequired, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 12 — Negative Impact Assessment" },
                "No action required."
            );
        }

        int impactCount = imp.NegativeImpacts?.Count ?? 0;
        bool hasImpacts = imp.NegativeImpacts != null && impactCount > 0 &&
                          imp.NegativeImpacts.All(i => !string.IsNullOrWhiteSpace(i.MitigationPlanRef));

        return new ComplianceFinding(
            "NPD-IMPACT-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.Applicable,
            hasImpacts ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            hasImpacts ? "Low" : "Medium", false, !hasImpacts,
            hasImpacts ? $"Identified {impactCount} negative impacts with mitigations." : "Negative impacts or mitigation plan references missing.",
            "All identified negative impacts must include mitigation plan references.",
            hasImpacts ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 12 — Negative Impact Assessment" },
            hasImpacts ? "No action required." : "Provide mitigation plan references for identified negative impacts."
        );
    }

    // 11. NPD-RISK-001
    private static ComplianceFinding EvaluateRiskFramework(ProposalComplianceInput input)
    {
        var rf = input.RiskFramework;
        int riskCount = rf?.Risks?.Count ?? 0;
        bool ok = rf != null && rf.Risks != null && riskCount > 0 &&
                  rf.Risks.All(r => !string.IsNullOrWhiteSpace(r.RiskId) && !string.IsNullOrWhiteSpace(r.Description));

        return new ComplianceFinding(
            "NPD-RISK-001", "1.0", RuleEvaluationType.RiskFramework, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            ok ? "Low" : "Medium", false, false,
            ok ? $"Risk framework defined with {riskCount} entries." : "Risk framework incomplete or missing.",
            "Project risk framework must define risks, descriptions, and mitigations.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 13 — Risk and Assumptions" },
            ok ? "No action required." : "Complete project risk and assumptions framework."
        );
    }

    // 12. NPD-DRR-001
    private static ComplianceFinding EvaluateDisasterRiskReduction(ProposalComplianceInput input)
    {
        var drr = input.DisasterRiskAssessment;
        if (drr == null || drr.Applicable == null)
        {
            return new ComplianceFinding(
                "NPD-DRR-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.Undetermined,
                RuleResultStatus.InsufficientInformation, "Low", false, true,
                "Disaster Risk Reduction applicability is undetermined from current input.",
                "DRR assessment required when project is in disaster-prone area.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 14 — Disaster Risk Reduction" },
                "Specify DRR applicability and provide assessment data if applicable."
            );
        }

        if (drr.Applicable == false)
        {
            return new ComplianceFinding(
                "NPD-DRR-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.NotApplicable,
                RuleResultStatus.NotApplicable, "Low", false, false,
                "Disaster Risk Reduction declared not applicable.",
                "N/A is acceptable when project is not in a disaster-prone area.",
                EvidenceStatus.NotRequired, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 14 — Disaster Risk Reduction" },
                "No action required."
            );
        }

        if (drr.AssessmentCompleted == false)
        {
            return new ComplianceFinding(
                "NPD-DRR-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.Applicable,
                RuleResultStatus.NonCompliant, "Medium", false, true,
                "Disaster Risk Reduction assessment is declared applicable but incomplete.",
                "DRR assessment must be completed when DRR is applicable.",
                EvidenceStatus.Missing, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 14 — Disaster Risk Reduction" },
                "Complete the Disaster Risk Reduction assessment."
            );
        }

        bool hasRef = !string.IsNullOrWhiteSpace(drr.AssessmentReference);
        bool hasHazards = drr.HazardsConsidered != null && drr.HazardsConsidered.Count > 0;
        bool hasMitigation = drr.MitigationMeasures != null && drr.MitigationMeasures.Count > 0;

        if (!hasRef || !hasHazards || !hasMitigation)
        {
            return new ComplianceFinding(
                "NPD-DRR-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                $"DRR assessment completed but missing details (Reference: {hasRef}, Hazards: {hasHazards}, Mitigation: {hasMitigation}).",
                "DRR assessment must provide reference number, hazards considered, and mitigation measures.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 14 — Disaster Risk Reduction" },
                "Supply missing DRR assessment reference, hazards, or mitigation measures."
            );
        }

        return new ComplianceFinding(
            "NPD-DRR-001", "1.0", RuleEvaluationType.ImpactAssessment, RuleApplicability.Applicable,
            RuleResultStatus.Compliant, "Low", false, false,
            $"DRR Assessment Reference: {drr.AssessmentReference}, Hazards: {drr.HazardsConsidered!.Count}, Mitigations: {drr.MitigationMeasures!.Count}",
            "Disaster Risk Reduction assessment and mitigation measures supplied.",
            EvidenceStatus.Provided, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 14 — Disaster Risk Reduction" },
            "No action required."
        );
    }

    // 13. NPD-ME-001
    private static ComplianceFinding EvaluateMonitoringEvaluationPlan(ProposalComplianceInput input)
    {
        var mp = input.MonitoringPlan;
        if (mp == null || mp.Kpis == null || mp.Kpis.Count == 0)
        {
            return new ComplianceFinding(
                "NPD-ME-001", "1.0", RuleEvaluationType.MonitoringAndEvaluation, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Monitoring plan KPIs missing.", "M&E plan must specify KPIs with baselines, targets, and responsible roles.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 15 — Monitoring and Evaluation" },
                "Provide M&E plan KPIs."
            );
        }

        bool complete = mp.Kpis.All(k => !string.IsNullOrWhiteSpace(k.KpiId) &&
                                         !string.IsNullOrWhiteSpace(k.UnitOfMeasure) &&
                                         k.TargetValue.HasValue &&
                                         !string.IsNullOrWhiteSpace(k.ResponsibleRole));

        return new ComplianceFinding(
            "NPD-ME-001", "1.0", RuleEvaluationType.MonitoringAndEvaluation, RuleApplicability.Applicable,
            complete ? RuleResultStatus.Compliant : RuleResultStatus.NonCompliant,
            complete ? "Low" : "Medium", false, false,
            complete ? $"M&E plan contains {mp.Kpis.Count} complete KPIs." : "Incomplete fields in M&E plan KPIs.",
            "KPIs must include unit of measure, target value, and responsible role.",
            complete ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 15 — Monitoring and Evaluation" },
            complete ? "No action required." : "Complete missing KPI units, targets, or responsible roles."
        );
    }

    // 14. NPD-BUD-001
    private static ComplianceFinding EvaluateBudgetCompleteness(ProposalComplianceInput input)
    {
        var b = input.Budget;
        if (b == null || !b.SubmittedProjectBudget.HasValue || b.CostComponents == null || b.CostComponents.Count == 0 || b.CostComponents.Any(c => !c.Amount.HasValue))
        {
            return new ComplianceFinding(
                "NPD-BUD-001", "1.0", RuleEvaluationType.Budget, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "High", false, true,
                "Submitted project budget or cost component amounts missing.", "Project budget must equal SUM(CostComponents).",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 16 — Project Budget" },
                "Supply submitted project budget and itemized cost component amounts."
            );
        }

        decimal calculated = b.CostComponents.Sum(c => c.Amount!.Value);
        decimal diff = Math.Abs(calculated - b.SubmittedProjectBudget.Value);
        bool balanced = diff <= 0.01m;

        return new ComplianceFinding(
            "NPD-BUD-001", "1.0", RuleEvaluationType.Budget, RuleApplicability.Applicable,
            balanced ? RuleResultStatus.Compliant : RuleResultStatus.NonCompliant,
            balanced ? "Low" : "High", false, false,
            $"SubmittedBudget: {b.SubmittedProjectBudget.Value}, CalculatedSum: {calculated}, Diff: {diff}",
            "Submitted budget must reconcile exactly with itemized cost component sum.",
            balanced ? EvidenceStatus.Provided : EvidenceStatus.Rejected, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 16 — Project Budget" },
            balanced ? "No action required." : "Reconcile submitted project budget with itemized cost components."
        );
    }

    // 15. NPD-FIN-001
    private static ComplianceFinding EvaluateFinancingReconciliation(ProposalComplianceInput input)
    {
        var fin = input.Financing;
        var b = input.Budget;

        if (fin == null || fin.FinancingSources == null || fin.FinancingSources.Count == 0 || fin.FinancingSources.Any(f => !f.Amount.HasValue) || b == null || !b.SubmittedProjectBudget.HasValue)
        {
            return new ComplianceFinding(
                "NPD-FIN-001", "1.0", RuleEvaluationType.Financing, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "High", false, true,
                "Financing sources, financing amounts, or project budget missing.", "Financing total must equal project budget.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 17 — Financing Plan" },
                "Provide financing sources breakdown and project budget."
            );
        }

        decimal totalFinancing = fin.FinancingSources.Sum(f => f.Amount!.Value);
        decimal gap = b.SubmittedProjectBudget.Value - totalFinancing;
        bool balanced = Math.Abs(gap) <= 0.01m;

        return new ComplianceFinding(
            "NPD-FIN-001", "1.0", RuleEvaluationType.Financing, RuleApplicability.Applicable,
            balanced ? RuleResultStatus.Compliant : RuleResultStatus.NonCompliant,
            balanced ? "Low" : "High", false, false,
            $"ProjectBudget: {b.SubmittedProjectBudget.Value}, TotalFinancing: {totalFinancing}, FundingGap: {gap}",
            "Financing total must reconcile with submitted project budget.",
            balanced ? EvidenceStatus.Provided : EvidenceStatus.Rejected, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 17 — Financing Plan" },
            balanced ? "No action required." : $"Resolve funding gap of {gap:N2} in the proposal's monetary unit."
        );
    }

    // 16. NPD-FIN-002
    private static ComplianceFinding EvaluateRevenueForecastConsistency(ProposalComplianceInput input)
    {
        var fin = input.Financing;
        if (fin == null || !fin.RevenueExpected.HasValue)
        {
            return new ComplianceFinding(
                "NPD-FIN-002", "1.0", RuleEvaluationType.Financing, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Low", false, false,
                "Financing revenue expected declaration missing.", "Revenue forecast consistency must be declared.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 17.2 — Revenue Forecast" },
                "Provide financing revenue expected declaration and forecast details."
            );
        }

        if (fin.RevenueExpected.Value && (!fin.RevenueForecastAmount.HasValue || fin.RevenueForecastAmount <= 0))
        {
            return new ComplianceFinding(
                "NPD-FIN-002", "1.0", RuleEvaluationType.CrossFieldConsistency, RuleApplicability.Applicable,
                RuleResultStatus.NonCompliant, "Medium", false, false,
                "RevenueExpected is true but RevenueForecastAmount is missing or zero.",
                "Positive revenue forecast amount required when RevenueExpected is true.",
                EvidenceStatus.Missing, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 17.2 — Revenue Forecast" },
                "Supply positive revenue forecast amount."
            );
        }

        if (!fin.RevenueExpected.Value && fin.RevenueForecastAmount.HasValue && fin.RevenueForecastAmount > 0)
        {
            return new ComplianceFinding(
                "NPD-FIN-002", "1.0", RuleEvaluationType.CrossFieldConsistency, RuleApplicability.Applicable,
                RuleResultStatus.RequiresHumanReview, "Medium", false, true,
                $"RevenueExpected is false but positive forecast amount ({fin.RevenueForecastAmount}) supplied.",
                "Revenue forecast amount should be absent when RevenueExpected is false.",
                EvidenceStatus.PendingVerification, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 17.2 — Revenue Forecast" },
                "Reconcile RevenueExpected flag with forecast amount."
            );
        }

        return new ComplianceFinding(
            "NPD-FIN-002", "1.0", RuleEvaluationType.Financing, RuleApplicability.Applicable,
            RuleResultStatus.Compliant, "Low", false, false,
            $"RevenueExpected: {fin.RevenueExpected.Value}, ForecastAmount: {fin.RevenueForecastAmount}",
            "Revenue expected status and forecast amounts are consistent.",
            EvidenceStatus.Provided, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 17.2 — Revenue Forecast" },
            "No action required."
        );
    }

    // 17. NPD-SUST-001
    private static ComplianceFinding EvaluateOperationMaintenanceCost(ProposalComplianceInput input)
    {
        var imp = input.Implementation;
        string? arr = imp?.OAndMArrangement;
        decimal? cost = imp?.OAndMCost;
        string? src = imp?.OAndMFundingSource;
        bool ok = !string.IsNullOrWhiteSpace(arr) && cost.HasValue && !string.IsNullOrWhiteSpace(src);

        return new ComplianceFinding(
            "NPD-SUST-001", "1.0", RuleEvaluationType.Sustainability, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            ok ? "Low" : "Medium", false, false,
            ok ? $"O&M Arrangement: {arr}, Cost: {cost}, Funding: {src}" : "O&M arrangement or cost breakdown missing.",
            "Post-completion operation and maintenance arrangements must be specified.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Items 17.3 & 22 — O&M Guidance" },
            ok ? "No action required." : "Provide O&M arrangements, costs, and funding sources."
        );
    }

    // 18. NPD-SOC-001
    private static ComplianceFinding EvaluateResettlementSafeguard(ProposalComplianceInput input)
    {
        var soc = input.SocialSafeguard;
        if (soc == null || soc.ResettlementApplicable == null)
        {
            return new ComplianceFinding(
                "NPD-SOC-001", "1.0", RuleEvaluationType.SocialSafeguard, RuleApplicability.Undetermined,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Resettlement applicability is undetermined.", "Declare whether resettlement applies.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 18 — Resettlement" },
                "Specify resettlement applicability."
            );
        }

        if (soc.ResettlementApplicable == false)
        {
            return new ComplianceFinding(
                "NPD-SOC-001", "1.0", RuleEvaluationType.SocialSafeguard, RuleApplicability.NotApplicable,
                RuleResultStatus.NotApplicable, "Low", false, false,
                "Resettlement declared not applicable.", "N/A acceptable when no resettlement required.",
                EvidenceStatus.NotRequired, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Item 18 — Resettlement" },
                "No action required."
            );
        }

        bool complete = soc.ResettlementCost.HasValue && soc.ResettlementCost > 0;

        return new ComplianceFinding(
            "NPD-SOC-001", "1.0", RuleEvaluationType.SocialSafeguard, RuleApplicability.Applicable,
            complete ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            complete ? "Low" : "High", false, !complete,
            complete ? $"Resettlement cost declared: {soc.ResettlementCost}" : "Resettlement cost details missing.",
            "Resettlement assessment and costs must be provided when applicable.",
            complete ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 18 — Resettlement" },
            complete ? "No action required." : "Provide resettlement plan and cost breakdown."
        );
    }

    // 19. NPD-SOC-002
    private static ComplianceFinding EvaluateGenderPerspective(ProposalComplianceInput input)
    {
        var soc = input.SocialSafeguard;
        bool? val = soc?.GenderConsidered;
        bool declared = val.HasValue;

        return new ComplianceFinding(
            "NPD-SOC-002", "1.0", RuleEvaluationType.SocialSafeguard, RuleApplicability.Applicable,
            declared ? RuleResultStatus.RequiresHumanReview : RuleResultStatus.InsufficientInformation,
            "Low", false, true,
            declared ? $"Gender consideration declared: {val}." : "Gender consideration status missing.",
            "Proposal should evaluate gender perspectives.",
            declared ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 19 — Gender Perspective" },
            declared ? "Human review recommended to assess gender perspective quality." : "Specify gender consideration status."
        );
    }

    // 20. NPD-SOC-003
    private static ComplianceFinding EvaluateAccessibilityConsideration(ProposalComplianceInput input)
    {
        var soc = input.SocialSafeguard;
        bool? val = soc?.AccessibilityConsidered;
        bool declared = val.HasValue;

        return new ComplianceFinding(
            "NPD-SOC-003", "1.0", RuleEvaluationType.SocialSafeguard, RuleApplicability.Applicable,
            declared ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            "Low", false, false,
            declared ? $"Accessibility considered: {val}." : "Accessibility consideration status missing.",
            "Proposal should evaluate accessibility for differently-abled persons.",
            declared ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 20 — Differently-Abled Persons" },
            declared ? "No action required." : "Specify accessibility consideration status."
        );
    }

    // 21. NPD-IMP-001
    private static ComplianceFinding EvaluateImplementationArrangements(ProposalComplianceInput input)
    {
        var imp = input.Implementation;
        if (imp == null || imp.Activities == null || imp.Activities.Count == 0)
        {
            return new ComplianceFinding(
                "NPD-IMP-001", "1.0", RuleEvaluationType.ImplementationReadiness, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Implementation activities missing.", "Main activities must have assigned responsible roles.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 21 — Implementation Arrangements" },
                "Provide implementation activities."
            );
        }

        var unassigned = imp.Activities.Where(a => string.IsNullOrWhiteSpace(a.ResponsibleRole)).ToList();
        bool ok = unassigned.Count == 0;

        return new ComplianceFinding(
            "NPD-IMP-001", "1.0", RuleEvaluationType.ImplementationReadiness, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.NonCompliant,
            ok ? "Low" : "Medium", false, false,
            ok ? $"All {imp.Activities.Count} activities assigned responsible roles." : $"Unassigned activities count: {unassigned.Count}",
            "Every implementation activity must have an assigned responsible organizational role.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 21 — Implementation Arrangements" },
            ok ? "No action required." : "Assign responsible roles to all implementation activities."
        );
    }

    // 22. NPD-SUST-002
    private static ComplianceFinding EvaluateSustainability(ProposalComplianceInput input)
    {
        var imp = input.Implementation;
        bool ok = imp != null && !string.IsNullOrWhiteSpace(imp.OAndMArrangement);

        return new ComplianceFinding(
            "NPD-SUST-002", "1.0", RuleEvaluationType.Sustainability, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.InsufficientInformation,
            ok ? "Low" : "Medium", false, false,
            ok ? "Sustainability and O&M arrangements specified." : "Sustainability arrangements missing.",
            "Proposal must include sustainability and O&M operational arrangements.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Missing, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 22 — Sustainability" },
            ok ? "No action required." : "Specify sustainability arrangements."
        );
    }

    // 23. NPD-ECO-001
    private static ComplianceFinding EvaluateEconomicFinancialAppraisal(ProposalComplianceInput input)
    {
        var eco = input.EconomicAppraisal;
        if (eco == null || string.IsNullOrWhiteSpace(eco.SelectedMethod))
        {
            return new ComplianceFinding(
                "NPD-ECO-001", "1.0", RuleEvaluationType.EconomicFinancialAppraisal, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Medium", false, true,
                "Economic/financial appraisal input missing.", "Proposal must specify selected economic/financial appraisal method.",
                EvidenceStatus.Missing, CalculationStatus.MissingInputs,
                NpdSource with { SourceSection = "Item 23 — Economic / Financial Appraisal" },
                "Specify economic/financial appraisal method and data."
            );
        }

        var results = new List<FinancialAppraisalResult>();

        // NPV
        if (eco.DiscountRate.HasValue && eco.CashFlows != null)
        {
            results.Add(FinancialAppraisalCalculator.RecalculateNpv(eco.DiscountRate, eco.CashFlows, eco.CashFlowPeriod, eco.SubmittedNpv));
        }

        // IRR
        if (eco.CashFlows != null && eco.CashFlows.Count >= 2)
        {
            results.Add(FinancialAppraisalCalculator.CalculateIrr(eco.CashFlows, eco.CashFlowPeriod, eco.SubmittedIrr));
        }

        // Payback
        if (eco.CashFlows != null && eco.CashFlows.Count > 0)
        {
            results.Add(FinancialAppraisalCalculator.CalculatePaybackPeriod(eco.CashFlows, eco.CashFlowPeriod, eco.SubmittedPaybackPeriod));
        }

        // CBR
        results.Add(FinancialAppraisalCalculator.CalculateCostBenefitRatio(eco.SubmittedPvBenefits, eco.SubmittedPvCosts, eco.SubmittedCbr, eco.CbrConvention));

        bool calcDone = results.Any(r => r.Status == CalculationStatus.Calculated);
        bool requiresConfirmation = results.Any(r => r.Status == CalculationStatus.RequiresStakeholderConfirmation);

        string summary = string.Join("; ", results.Select(r => $"{r.MethodName}: {r.Status}"));

        return new ComplianceFinding(
            "NPD-ECO-001", "1.0", RuleEvaluationType.EconomicFinancialAppraisal, RuleApplicability.Applicable,
            requiresConfirmation ? RuleResultStatus.RequiresHumanReview : RuleResultStatus.Compliant,
            "Low", false, true,
            $"Method: {eco.SelectedMethod}. Summaries: {summary}",
            "Economic/financial appraisal values are recalculated. Cost-Benefit Ratio decision rule requires stakeholder confirmation.",
            EvidenceStatus.Provided,
            requiresConfirmation ? CalculationStatus.RequiresStakeholderConfirmation : CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Item 23 — Economic / Financial Appraisal" },
            "Review recalculated economic/financial appraisal values. Stakeholder confirmation required for Cost-Benefit Ratio threshold."
        );
    }

    // 24. NPD-EVD-001
    private static ComplianceFinding EvaluateEvidenceTraceability(ProposalComplianceInput input)
    {
        var refs = input.EvidenceReferences;
        if (refs == null || refs.Count == 0)
        {
            return new ComplianceFinding(
                "NPD-EVD-001", "1.0", RuleEvaluationType.AnnexTraceability, RuleApplicability.Applicable,
                RuleResultStatus.InsufficientInformation, "Low", false, false,
                "No supporting evidence references supplied.", "Declared facts requiring supporting evidence must include reference IDs.",
                EvidenceStatus.Missing, CalculationStatus.Calculated,
                NpdSource with { SourceSection = "Guidance on Annexes" },
                "Supply supporting evidence reference IDs."
            );
        }

        var duplicates = refs.GroupBy(r => r.EvidenceId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        bool ok = duplicates.Count == 0 && refs.All(r => !string.IsNullOrWhiteSpace(r.ReferenceNumber));

        return new ComplianceFinding(
            "NPD-EVD-001", "1.0", RuleEvaluationType.AnnexTraceability, RuleApplicability.Applicable,
            ok ? RuleResultStatus.Compliant : RuleResultStatus.NonCompliant,
            ok ? "Low" : "Medium", false, false,
            ok ? $"Supplied {refs.Count} valid evidence references." : $"Duplicate evidence IDs count: {duplicates.Count}",
            "Supporting evidence references must be non-empty and unique.",
            ok ? EvidenceStatus.Provided : EvidenceStatus.Rejected, CalculationStatus.Calculated,
            NpdSource with { SourceSection = "Guidance on Annexes" },
            ok ? "No action required." : "Ensure evidence reference numbers are unique and non-empty."
        );
    }
}

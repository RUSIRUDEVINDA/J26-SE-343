using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeder for populating authentic National Planning Department (NPD) January 2019 Operational Manual rules
/// into the PostgreSQL regulatory knowledge database tables.
/// </summary>
public static class RegulatoryKnowledgeSeeder
{
    public static async Task SeedAsync(GovernanceIntelligenceDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));

        if (await dbContext.ComplianceRules.AnyAsync(cancellationToken))
        {
            return; // Idempotent check: already seeded
        }

        var source = new RegulatorySourceEntity
        {
            Id = Guid.NewGuid(),
            SourceType = "GovernmentOperationalManual",
            Authority = "Department of National Planning",
            DocumentTitle = "Operational Manual / Project Submission Format",
            DocumentVersion = "January 2019",
            GazetteNumber = null,
            PublishedDate = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveFrom = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = null,
            DocumentReference = "NPD-OM-2019-V1",
            Checksum = "2019.1",
            IsActive = true
        };

        dbContext.RegulatorySources.Add(source);

        var rules = new List<ComplianceRuleEntity>
        {
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-LOC-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 3 — Project Location", Category = "Completeness", Description = "Project location hierarchy check", RuleType = "Completeness", CalculationKey = "ProjectLocationCheck", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-LAND-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 4 — Land Requirement", Category = "Completeness", Description = "Land requirement extent & allocation check", RuleType = "Completeness", CalculationKey = "LandRequirementCheck", Severity = "High", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-READY-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 5 — Project Preliminary Activities", Category = "ProjectReadiness", Description = "Project preliminary studies check", RuleType = "ProjectReadiness", CalculationKey = "ProjectReadinessCheck", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-CONS-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Items 6–16 — General Coherence Guidance", Category = "CrossFieldConsistency", Description = "Cross-field consistency check", RuleType = "CrossFieldConsistency", CalculationKey = "CrossFieldCoherence", Severity = "High", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-RAT-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 7 — Rationale of Project", Category = "Completeness", Description = "Project problem & rationale check", RuleType = "Completeness", CalculationKey = "ProblemRationaleCheck", Severity = "Low", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-POL-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 8 — Relationship to National Policies", Category = "PolicyAlignmentDeclaration", Description = "National policy alignment check", RuleType = "PolicyAlignmentDeclaration", CalculationKey = "PolicyAlignmentCheck", Severity = "Low", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-STK-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 9 — Stakeholder Coordination", Category = "StakeholderCoordinationEvidence", Description = "Stakeholder consultation check", RuleType = "StakeholderCoordinationEvidence", CalculationKey = "StakeholderConsultation", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-STK-002", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 9 — Overlap Consideration", Category = "Completeness", Description = "Duplication & overlap check", RuleType = "Completeness", CalculationKey = "DuplicationAssessment", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-RESULT-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 10 — Logic Model Guidance", Category = "ResultsFrameworkConsistency", Description = "Results framework logic check", RuleType = "ResultsFrameworkConsistency", CalculationKey = "ResultsFrameworkLogic", Severity = "High", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-IMPACT-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 12 — Negative Impact Assessment", Category = "ImpactAssessment", Description = "Negative impacts check", RuleType = "ImpactAssessment", CalculationKey = "NegativeImpactCheck", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-RISK-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 13 — Risk and Assumptions", Category = "RiskFramework", Description = "Risk framework check", RuleType = "RiskFramework", CalculationKey = "RiskFrameworkCheck", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-DRR-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 14 — Disaster Risk Reduction", Category = "ImpactAssessment", Description = "DRR assessment check", RuleType = "ImpactAssessment", CalculationKey = "DisasterRiskReduction", Severity = "Low", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-ME-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 15 — Monitoring and Evaluation", Category = "MonitoringAndEvaluation", Description = "M&E plan KPIs check", RuleType = "MonitoringAndEvaluation", CalculationKey = "MonitoringPlanKpis", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-BUD-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 16 — Project Budget", Category = "Budget", Description = "Budget reconciliation check", RuleType = "Budget", CalculationKey = "BudgetReconciliation", Severity = "High", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-FIN-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 17 — Financing Plan", Category = "Financing", Description = "Financing reconciliation check", RuleType = "Financing", CalculationKey = "FinancingReconciliation", Severity = "High", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-FIN-002", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 17.2 — Revenue Forecast", Category = "Financing", Description = "Revenue forecast consistency check", RuleType = "Financing", CalculationKey = "RevenueForecastCheck", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-SUST-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Items 17.3 & 22 — O&M Guidance", Category = "Sustainability", Description = "Operation & maintenance cost check", RuleType = "Sustainability", CalculationKey = "OperationMaintenanceCost", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-SOC-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 18 — Resettlement", Category = "SocialSafeguard", Description = "Resettlement safeguard check", RuleType = "SocialSafeguard", CalculationKey = "ResettlementSafeguard", Severity = "High", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-SOC-002", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 19 — Gender Perspective", Category = "SocialSafeguard", Description = "Gender perspective check", RuleType = "SocialSafeguard", CalculationKey = "GenderPerspectiveCheck", Severity = "Low", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-SOC-003", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 20 — Differently-Abled Persons", Category = "SocialSafeguard", Description = "Accessibility check", RuleType = "SocialSafeguard", CalculationKey = "AccessibilityCheck", Severity = "Low", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-IMP-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 21 — Implementation Arrangements", Category = "ImplementationReadiness", Description = "Implementation arrangements check", RuleType = "ImplementationReadiness", CalculationKey = "ImplementationRoles", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-SUST-002", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 22 — Sustainability", Category = "Sustainability", Description = "Sustainability plan check", RuleType = "Sustainability", CalculationKey = "SustainabilityArrangements", Severity = "Medium", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-ECO-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Item 23 — Economic / Financial Appraisal", Category = "EconomicFinancialAppraisal", Description = "Economic financial appraisal recalculations", RuleType = "EconomicFinancialAppraisal", CalculationKey = "EconomicAppraisal", Severity = "Low", IsBlocking = false, Enabled = true },
            new() { Id = Guid.NewGuid(), RuleCode = "NPD-EVD-001", RuleVersion = "1.0", SourceId = source.Id, SourceSection = "Guidance on Annexes", Category = "AnnexTraceability", Description = "Evidence traceability check", RuleType = "AnnexTraceability", CalculationKey = "EvidenceTraceability", Severity = "Low", IsBlocking = false, Enabled = true }
        };

        dbContext.ComplianceRules.AddRange(rules);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

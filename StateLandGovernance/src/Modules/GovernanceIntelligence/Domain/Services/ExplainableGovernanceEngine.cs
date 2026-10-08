using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Domain.Constants;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Deterministic domain engine that synthesizes transparent, human-readable governance explanations.
/// </summary>
public sealed class ExplainableGovernanceEngine : IExplainableGovernanceEngine
{
    public const string DefaultDisclaimer = "This governance explanation is an automated decision-support finding for human review and does not constitute a finding of legal wrongdoing, legal fraud, or disciplinary guilt.";

    public GovernanceExplanationResult SynthesizeExplanation(GovernanceExplanationInput input, DateTime evaluationTimestamp)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input), "Governance explanation input cannot be null.");
        }

        var items = new List<GovernanceExplanationItem>();

        // 1. Process Compliance Evidence
        if (input.ComplianceEvidence != null)
        {
            ProcessComplianceEvidence(input.SubjectId, input.ComplianceEvidence, items);
        }

        // 2. Process Conflict Evidence
        if (input.ConflictEvidence != null)
        {
            ProcessConflictEvidence(input.SubjectId, input.ConflictEvidence, items);
        }

        // 3. Process Risk Evidence
        if (input.RiskEvidence != null)
        {
            ProcessRiskEvidence(input.SubjectId, input.RiskEvidence, items);
        }

        // 4. Handle No Elevated Issues Case
        if (items.Count == 0)
        {
            string itemId = ComputeItemId("COMP", input.SubjectId, EngineType.RegulatoryCompliance, GovernanceReasonCodes.NoElevatedGovernanceIssues, "NO_ELEVATED_ISSUES");
            items.Add(new GovernanceExplanationItem(
                itemId,
                EngineType.RegulatoryCompliance,
                "Clear",
                GovernanceExplanationSeverity.Low,
                GovernanceReasonCodes.NoElevatedGovernanceIssues,
                "No Elevated Governance Issues Detected",
                $"Governance evaluation for subject '{input.SubjectId}' identified no regulatory compliance violations, decision conflicts, or elevated risk indicators.",
                new[] { "All evaluated governance dimensions returned clear / compliant results." },
                "No immediate administrative action required. Standard monitoring applies.",
                requiresHumanAttention: false));
        }

        // 5. Deterministic Ordering Strategy
        // Primary: Severity Descending, Secondary: Source Engine Ascending, Tertiary: Reason Code Ascending
        var orderedItems = items
            .OrderByDescending(i => (int)i.Severity)
            .ThenBy(i => (int)i.SourceEngine)
            .ThenBy(i => i.ReasonCode, StringComparer.Ordinal)
            .ThenBy(i => i.ItemId, StringComparer.Ordinal)
            .ToList();

        // 6. Calculate Aggregates
        var overallSeverity = orderedItems.Max(i => i.Severity);
        bool requiresHumanReview = orderedItems.Any(i => i.RequiresHumanAttention);

        // 7. Deterministic Explanation ID
        string sortedItemKeys = string.Join("|", orderedItems.Select(i => i.ItemId));
        string explanationId = ComputeResultId(input.SubjectId, evaluationTimestamp, sortedItemKeys);

        return new GovernanceExplanationResult(
            explanationId,
            input.SubjectId,
            overallSeverity,
            requiresHumanReview,
            orderedItems,
            evaluationTimestamp,
            DefaultDisclaimer);
    }

    private static void ProcessComplianceEvidence(string subjectId, ComplianceExplanationEvidence compliance, List<GovernanceExplanationItem> items)
    {
        // Sort violated rules deterministically
        var sortedViolations = compliance.ViolatedRules
            .OrderBy(v => v.RuleCode, StringComparer.Ordinal)
            .ToList();

        foreach (var v in sortedViolations)
        {
            string normalizedMsg = (v.SummaryMessage ?? string.Empty).Trim();
            string evidenceKey = $"{v.RuleCode}:{v.RuleCategory}:{normalizedMsg}";
            string itemId = ComputeItemId("COMP", subjectId, EngineType.RegulatoryCompliance, GovernanceReasonCodes.ComplianceRuleViolation, evidenceKey);

            var severity = GovernanceExplanationSeverity.High;
            string explanationText = $"Regulatory compliance evaluation flagged rule violation '{v.RuleCode}' (Category: {v.RuleCategory}) for governance subject '{subjectId}'. Summary: {normalizedMsg}.";
            string recommendedAction = "Review proposed land lease action against statutory limits and request compliance rectification.";

            items.Add(new GovernanceExplanationItem(
                itemId,
                EngineType.RegulatoryCompliance,
                "NonCompliant",
                severity,
                GovernanceReasonCodes.ComplianceRuleViolation,
                $"Regulatory Violation: {v.RuleCode}",
                explanationText,
                new[] { $"Rule Code: {v.RuleCode}", $"Category: {v.RuleCategory}", $"Details: {normalizedMsg}" },
                recommendedAction,
                requiresHumanAttention: true));
        }

        // Unsatisfied conditions
        var sortedConditions = compliance.UnsatisfiedConditions
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        foreach (var condition in sortedConditions)
        {
            string itemId = ComputeItemId("COMP", subjectId, EngineType.RegulatoryCompliance, GovernanceReasonCodes.ComplianceConditionUnsatisfied, condition);

            items.Add(new GovernanceExplanationItem(
                itemId,
                EngineType.RegulatoryCompliance,
                "ConditionUnsatisfied",
                GovernanceExplanationSeverity.Moderate,
                GovernanceReasonCodes.ComplianceConditionUnsatisfied,
                "Unsatisfied Governance Condition",
                $"Regulatory compliance evaluation identified an unsatisfied prerequisite condition for subject '{subjectId}': {condition}.",
                new[] { $"Condition: {condition}" },
                "Verify required documentation and complete mandatory prerequisites prior to approval.",
                requiresHumanAttention: true));
        }
    }

    private static void ProcessConflictEvidence(string subjectId, ConflictExplanationEvidence conflict, List<GovernanceExplanationItem> items)
    {
        var sortedConflicts = conflict.Conflicts
            .OrderBy(c => c.ConflictType, StringComparer.Ordinal)
            .ThenBy(c => c.EvidenceRuleCode, StringComparer.Ordinal)
            .ToList();

        foreach (var c in sortedConflicts)
        {
            string evidenceKey = $"{c.ConflictType}:{c.EvidenceRuleCode}:{c.SummaryMessage}";
            string itemId = ComputeItemId("CONF", subjectId, EngineType.GovernanceConflict, GovernanceReasonCodes.ConflictingGovernanceDecisions, evidenceKey);

            string explanationText = $"Governance conflict detection engine identified a decision conflict of type '{c.ConflictType}' for subject '{subjectId}'. Details: {c.SummaryMessage}. Triggered rule: {c.EvidenceRuleCode}.";

            items.Add(new GovernanceExplanationItem(
                itemId,
                EngineType.GovernanceConflict,
                "ConflictDetected",
                c.Severity,
                GovernanceReasonCodes.ConflictingGovernanceDecisions,
                $"Decision Conflict: {c.ConflictType}",
                explanationText,
                new[] { $"Conflict Type: {c.ConflictType}", $"Triggered Rule: {c.EvidenceRuleCode}", $"Summary: {c.SummaryMessage}" },
                c.RecommendedAction ?? "Convene inter-institutional review panel to reconcile conflicting decisions.",
                requiresHumanAttention: c.Severity >= GovernanceExplanationSeverity.Moderate));
        }
    }

    private static void ProcessRiskEvidence(string subjectId, RiskExplanationEvidence risk, List<GovernanceExplanationItem> items)
    {

        var sortedIndicators = risk.TriggeredIndicators
            .OrderByDescending(i => (int)i.Severity)
            .ThenBy(i => i.IndicatorCode, StringComparer.Ordinal)
            .ToList();

        foreach (var ind in sortedIndicators)
        {
            string reasonCode = MapIndicatorReasonCode(ind.IndicatorCode, ind.Category);
            string evidenceKey = $"{ind.IndicatorCode}:{ind.Category}:{ind.EvidenceSummary}";
            string itemId = ComputeItemId("RISK", subjectId, EngineType.RiskAndCorruption, reasonCode, evidenceKey);

            string explanationText = $"Governance risk assessment identified an elevated indicator '{ind.IndicatorCode}' (Category: {ind.Category}) for subject '{subjectId}'. Score contribution: {ind.Severity}. Finding: {ind.EvidenceSummary}.";

            items.Add(new GovernanceExplanationItem(
                itemId,
                EngineType.RiskAndCorruption,
                "ElevatedRiskIndicator",
                ind.Severity,
                reasonCode,
                $"Risk Indicator: {ind.IndicatorCode}",
                explanationText,
                new[] { $"Indicator: {ind.IndicatorCode}", $"Category: {ind.Category}", $"Triggered Rule: {ind.TriggeredRule}", $"Evidence: {ind.EvidenceSummary}" },
                ind.RecommendedAction ?? "Perform independent administrative audit of decision history and authorization records.",
                requiresHumanAttention: ind.Severity >= GovernanceExplanationSeverity.Moderate));
        }
    }

    private static string MapIndicatorReasonCode(string indicatorCode, string category)
    {
        if (indicatorCode.Contains("OVERRIDE", StringComparison.OrdinalIgnoreCase) || category.Contains("Override", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceReasonCodes.RepeatedOverridePattern;
        }

        if (indicatorCode.Contains("COMPLAINT", StringComparison.OrdinalIgnoreCase) || category.Contains("Complaint", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceReasonCodes.UnverifiedComplaintConcentration;
        }

        if (indicatorCode.Contains("VALIDATION", StringComparison.OrdinalIgnoreCase) || category.Contains("Validation", StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceReasonCodes.InstitutionalValidationFailure;
        }

        return GovernanceReasonCodes.HighGovernanceRisk;
    }

    private static string ComputeItemId(string prefix, string subjectId, EngineType sourceEngine, string reasonCode, string evidenceKey)
    {
        string raw = $"{subjectId.Trim()}|{(int)sourceEngine}|{reasonCode.Trim()}|{evidenceKey.Trim()}";
        byte[] bytes = Encoding.UTF8.GetBytes(raw);
        byte[] hash = SHA256.HashData(bytes);
        string hex16 = Convert.ToHexString(hash).ToLowerInvariant()[..16];
        return $"EXP-{prefix}-{hex16}";
    }

    private static string ComputeResultId(string subjectId, DateTime timestamp, string sortedItemKeys)
    {
        string raw = $"{subjectId.Trim()}|{timestamp:O}|{sortedItemKeys}";
        byte[] bytes = Encoding.UTF8.GetBytes(raw);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}

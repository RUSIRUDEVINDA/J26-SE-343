using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Configurable research options for the Governance Risk & Corruption Intelligence Engine.
/// Live within the domain layer to maintain zero external I/O or framework dependencies.
/// </summary>
public sealed record GovernanceRiskEngineOptions(
    int RapidApprovalThresholdHours = 24,
    int RapidApprovalMinCount = 3,
    int RapidApprovalWeight = 15,
    int RepeatedOverrideMinCount = 2,
    int RepeatedOverrideWeight = 25,
    int RegulatoryViolationMinCount = 2,
    int RegulatoryViolationWeight = 20,
    int GovernanceConflictMinCount = 2,
    int GovernanceConflictWeight = 20,
    int VerifiedComplaintWeight = 15,
    int ValidationFailureMinCount = 2,
    int ValidationFailureWeight = 20,
    int OffHoursDecisionMinCount = 2,
    int OffHoursDecisionWeight = 15,
    int ConcentrationMinCount = 4,
    int ConcentrationWeight = 15,
    int ExceptionMinCount = 2,
    int ExceptionWeight = 15,
    int CrossEvidenceMinCategoryCount = 3,
    int CrossEvidenceBonusWeight = 15
)
{
    public static GovernanceRiskEngineOptions Default { get; } = new();
}

/// <summary>
/// Domain service implementation of the deterministic Governance Risk & Corruption Intelligence Engine.
/// </summary>
public sealed class GovernanceRiskEngine : IGovernanceRiskEngine
{
    private readonly GovernanceRiskEngineOptions _options;

    public GovernanceRiskEngine() : this(GovernanceRiskEngineOptions.Default)
    {
    }

    public GovernanceRiskEngine(GovernanceRiskEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public GovernanceRiskAssessmentResult AssessRisk(GovernanceRiskEvaluationInput input, DateTime evaluationTimestamp)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input), "Risk evaluation input cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(input.SubjectId))
        {
            throw new ArgumentException("Subject identifier cannot be null or whitespace.", nameof(input));
        }

        var utcTimestamp = evaluationTimestamp.Kind == DateTimeKind.Utc ? evaluationTimestamp : evaluationTimestamp.ToUniversalTime();
        var normSubjectId = Normalize(input.SubjectId);

        // 1. Deduplicate and normalize input collections deterministically
        var decisions = NormalizeAndDeduplicateDecisions(input.DecisionHistory);
        var violations = NormalizeAndDeduplicateViolations(input.ComplianceViolations);
        var conflicts = NormalizeAndDeduplicateConflicts(input.GovernanceConflicts);
        var complaints = NormalizeAndDeduplicateComplaints(input.Complaints);
        var validations = NormalizeAndDeduplicateValidations(input.InstitutionalValidations);

        var indicators = new List<GovernanceRiskIndicator>();

        // 2. Evaluate Category 1: ApprovalPatternAnomaly
        EvaluateApprovalPatternAnomaly(normSubjectId, decisions, indicators);

        // 3. Evaluate Category 2: RepeatedOverrideRisk
        EvaluateRepeatedOverrideRisk(normSubjectId, decisions, indicators);

        // 4. Evaluate Category 3: RegulatoryViolationPattern
        EvaluateRegulatoryViolationPattern(normSubjectId, violations, indicators);

        // 5. Evaluate Category 4: ConflictRecurrenceRisk
        EvaluateConflictRecurrenceRisk(normSubjectId, conflicts, indicators);

        // 6. Evaluate Category 5: ComplaintRiskIndicator
        EvaluateComplaintRiskIndicator(normSubjectId, complaints, indicators);

        // 7. Evaluate Category 6: InstitutionalValidationRisk
        EvaluateInstitutionalValidationRisk(normSubjectId, validations, indicators);

        // 8. Evaluate Category 7: TemporalActivityAnomaly
        EvaluateTemporalActivityAnomaly(normSubjectId, decisions, indicators);

        // 9. Evaluate Category 8: DecisionConcentrationRisk
        EvaluateDecisionConcentrationRisk(normSubjectId, decisions, indicators);

        // 10. Evaluate Category 9: ExceptionFrequencyRisk
        EvaluateExceptionFrequencyRisk(normSubjectId, decisions, indicators);

        // 11. Evaluate Category 10: CrossEvidenceRisk (Compound risk bonus)
        EvaluateCrossEvidenceRisk(normSubjectId, indicators);

        // 12. Calculate overall score and severity
        int rawScore = indicators.Sum(i => i.ScoreContribution);
        int overallScore = Math.Min(100, Math.Max(0, rawScore));
        var overallSeverity = DeriveSeverity(overallScore);
        bool requiresHumanReview = overallScore >= 25;

        // 13. Sort indicators deterministically
        var orderedIndicators = indicators
            .OrderByDescending(i => i.ScoreContribution)
            .ThenBy(i => i.Category)
            .ThenBy(i => i.IndicatorId, StringComparer.Ordinal)
            .ToList();

        return new GovernanceRiskAssessmentResult(
            normSubjectId,
            overallScore,
            overallSeverity,
            orderedIndicators,
            utcTimestamp,
            requiresHumanReview
        );
    }

    private void EvaluateApprovalPatternAnomaly(string subjectId, List<DecisionHistoryObservation> decisions, List<GovernanceRiskIndicator> indicators)
    {
        var approvals = decisions.Where(d => string.Equals(d.DecisionType, "Approval", StringComparison.OrdinalIgnoreCase)).ToList();
        if (approvals.Count < _options.RapidApprovalMinCount) return;

        // Check if rapid approval threshold window is exceeded
        var sorted = approvals.OrderBy(a => a.Timestamp).ToList();
        bool rapidDetected = false;
        for (int i = 0; i <= sorted.Count - _options.RapidApprovalMinCount; i++)
        {
            var windowStart = sorted[i].Timestamp;
            var windowEnd = sorted[i + _options.RapidApprovalMinCount - 1].Timestamp;
            if ((windowEnd - windowStart).TotalHours <= _options.RapidApprovalThresholdHours)
            {
                rapidDetected = true;
                break;
            }
        }

        if (rapidDetected)
        {
            var actors = approvals.Select(a => a.OfficerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var evidenceKeys = string.Join(";", approvals.Select(a => a.DecisionId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.ApprovalPatternAnomaly, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.ApprovalPatternAnomaly,
                _options.RapidApprovalWeight,
                DeriveSeverity(_options.RapidApprovalWeight),
                subjectId,
                actors,
                $"{approvals.Count} approvals detected within a {_options.RapidApprovalThresholdHours}-hour window.",
                "RULE-RISK-001 (Rapid Approval Pattern Anomaly)",
                $"Governance subject '{subjectId}' exhibits {approvals.Count} rapid decision approvals within a {_options.RapidApprovalThresholdHours}-hour window.",
                GetRecommendedAction(_options.RapidApprovalWeight)
            ));
        }
    }

    private void EvaluateRepeatedOverrideRisk(string subjectId, List<DecisionHistoryObservation> decisions, List<GovernanceRiskIndicator> indicators)
    {
        var overrides = decisions.Where(d => d.IsOverride || IsOverrideType(d.DecisionType)).ToList();
        if (overrides.Count >= _options.RepeatedOverrideMinCount)
        {
            var actors = overrides.Select(d => d.OfficerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var evidenceKeys = string.Join(";", overrides.Select(d => d.DecisionId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.RepeatedOverrideRisk, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.RepeatedOverrideRisk,
                _options.RepeatedOverrideWeight,
                DeriveSeverity(_options.RepeatedOverrideWeight),
                subjectId,
                actors,
                $"{overrides.Count} decision overrides/reversals detected.",
                "RULE-RISK-002 (Repeated Decision Overrides)",
                $"Governance subject '{subjectId}' exhibits {overrides.Count} decision overrides/reversals within the observed evaluation history.",
                GetRecommendedAction(_options.RepeatedOverrideWeight)
            ));
        }
    }

    private void EvaluateRegulatoryViolationPattern(string subjectId, List<ComplianceViolationEvidence> violations, List<GovernanceRiskIndicator> indicators)
    {
        if (violations.Count >= _options.RegulatoryViolationMinCount)
        {
            var actors = violations.Select(v => v.RuleId).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
            var evidenceKeys = string.Join(";", violations.Select(v => v.ViolationId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.RegulatoryViolationPattern, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.RegulatoryViolationPattern,
                _options.RegulatoryViolationWeight,
                DeriveSeverity(_options.RegulatoryViolationWeight),
                subjectId,
                actors,
                $"{violations.Count} regulatory compliance violations recorded.",
                "RULE-RISK-003 (Regulatory Compliance Violation Pattern)",
                $"Governance subject '{subjectId}' has {violations.Count} recorded regulatory compliance violations requiring compliance review.",
                GetRecommendedAction(_options.RegulatoryViolationWeight)
            ));
        }
    }

    private void EvaluateConflictRecurrenceRisk(string subjectId, List<GovernanceConflictEvidence> conflicts, List<GovernanceRiskIndicator> indicators)
    {
        bool hasCritical = conflicts.Any(c => string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
        if (conflicts.Count >= _options.GovernanceConflictMinCount || hasCritical)
        {
            var actors = conflicts.Select(c => c.ConflictType).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            var evidenceKeys = string.Join(";", conflicts.Select(c => c.ConflictId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.ConflictRecurrenceRisk, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.ConflictRecurrenceRisk,
                _options.GovernanceConflictWeight,
                DeriveSeverity(_options.GovernanceConflictWeight),
                subjectId,
                actors,
                $"{conflicts.Count} governance conflicts recorded (Critical: {hasCritical}).",
                "RULE-RISK-004 (Governance Conflict Recurrence Pattern)",
                $"Governance subject '{subjectId}' exhibits recurring or high-severity governance conflicts across institutional decisions.",
                GetRecommendedAction(_options.GovernanceConflictWeight)
            ));
        }
    }

    private void EvaluateComplaintRiskIndicator(string subjectId, List<ComplaintObservation> complaints, List<GovernanceRiskIndicator> indicators)
    {
        var activeComplaints = complaints.Where(c => 
            string.Equals(c.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.VerificationStatus, "UnderReview", StringComparison.OrdinalIgnoreCase)).ToList();

        if (activeComplaints.Count > 0)
        {
            var actors = activeComplaints.Select(c => c.Category).Where(cat => !string.IsNullOrWhiteSpace(cat)).Distinct().ToList();
            var evidenceKeys = string.Join(";", activeComplaints.Select(c => c.ComplaintId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.ComplaintRiskIndicator, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.ComplaintRiskIndicator,
                _options.VerifiedComplaintWeight,
                DeriveSeverity(_options.VerifiedComplaintWeight),
                subjectId,
                actors,
                $"{activeComplaints.Count} verified or under-review stakeholder complaint indicators recorded.",
                "RULE-RISK-005 (Stakeholder Complaint Risk Indicator)",
                $"Governance subject '{subjectId}' has {activeComplaints.Count} verified or active citizen/stakeholder complaint indicators requiring procedural review.",
                GetRecommendedAction(_options.VerifiedComplaintWeight)
            ));
        }
    }

    private void EvaluateInstitutionalValidationRisk(string subjectId, List<InstitutionalValidationObservation> validations, List<GovernanceRiskIndicator> indicators)
    {
        var failures = validations.Where(v => !v.IsValidated).ToList();
        if (failures.Count >= _options.ValidationFailureMinCount)
        {
            var actors = failures.Select(v => v.InstitutionId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var evidenceKeys = string.Join(";", failures.Select(f => f.ValidationId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.InstitutionalValidationRisk, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.InstitutionalValidationRisk,
                _options.ValidationFailureWeight,
                DeriveSeverity(_options.ValidationFailureWeight),
                subjectId,
                actors,
                $"{failures.Count} institutional validation/concurrence failures recorded.",
                "RULE-RISK-006 (Institutional Validation Failure Pattern)",
                $"Governance subject '{subjectId}' has {failures.Count} recorded institutional validation/concurrence failures.",
                GetRecommendedAction(_options.ValidationFailureWeight)
            ));
        }
    }

    private void EvaluateTemporalActivityAnomaly(string subjectId, List<DecisionHistoryObservation> decisions, List<GovernanceRiskIndicator> indicators)
    {
        // Off-hours decisions: outside 07:00 UTC to 19:00 UTC
        var offHours = decisions.Where(d => d.Timestamp.Hour < 7 || d.Timestamp.Hour >= 19).ToList();
        if (offHours.Count >= _options.OffHoursDecisionMinCount)
        {
            var actors = offHours.Select(d => d.OfficerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var evidenceKeys = string.Join(";", offHours.Select(d => d.DecisionId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.TemporalActivityAnomaly, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.TemporalActivityAnomaly,
                _options.OffHoursDecisionWeight,
                DeriveSeverity(_options.OffHoursDecisionWeight),
                subjectId,
                actors,
                $"{offHours.Count} decisions recorded outside standard business operating hours.",
                "RULE-RISK-007 (Temporal Decision Activity Anomaly)",
                $"Governance subject '{subjectId}' has {offHours.Count} decisions recorded outside standard operational hours.",
                GetRecommendedAction(_options.OffHoursDecisionWeight)
            ));
        }
    }

    private void EvaluateDecisionConcentrationRisk(string subjectId, List<DecisionHistoryObservation> decisions, List<GovernanceRiskIndicator> indicators)
    {
        var officerGroups = decisions
            .Where(d => !string.IsNullOrWhiteSpace(d.OfficerId))
            .GroupBy(d => Normalize(d.OfficerId))
            .Where(g => g.Count() >= _options.ConcentrationMinCount)
            .ToList();

        if (officerGroups.Count > 0)
        {
            var maxGroup = officerGroups.OrderByDescending(g => g.Count()).First();
            var officerId = maxGroup.First().OfficerId;
            var evidenceKeys = string.Join(";", maxGroup.Select(d => d.DecisionId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.DecisionConcentrationRisk, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.DecisionConcentrationRisk,
                _options.ConcentrationWeight,
                DeriveSeverity(_options.ConcentrationWeight),
                subjectId,
                new[] { officerId },
                $"{maxGroup.Count()} decisions concentrated with officer '{officerId}'.",
                "RULE-RISK-008 (High Decision Concentration Pattern)",
                $"Governance subject '{subjectId}' has {maxGroup.Count()} decisions concentrated with officer '{officerId}'.",
                GetRecommendedAction(_options.ConcentrationWeight)
            ));
        }
    }

    private void EvaluateExceptionFrequencyRisk(string subjectId, List<DecisionHistoryObservation> decisions, List<GovernanceRiskIndicator> indicators)
    {
        var exceptions = decisions.Where(d => d.IsException || string.Equals(d.DecisionType, "Exception", StringComparison.OrdinalIgnoreCase)).ToList();
        if (exceptions.Count >= _options.ExceptionMinCount)
        {
            var actors = exceptions.Select(d => d.OfficerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var evidenceKeys = string.Join(";", exceptions.Select(d => d.DecisionId));
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.ExceptionFrequencyRisk, subjectId, evidenceKeys);

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.ExceptionFrequencyRisk,
                _options.ExceptionWeight,
                DeriveSeverity(_options.ExceptionWeight),
                subjectId,
                actors,
                $"{exceptions.Count} discretionary policy exception decisions recorded.",
                "RULE-RISK-009 (Exception Frequency Risk Pattern)",
                $"Governance subject '{subjectId}' exhibits {exceptions.Count} discretionary policy exception decisions.",
                GetRecommendedAction(_options.ExceptionWeight)
            ));
        }
    }

    private void EvaluateCrossEvidenceRisk(string subjectId, List<GovernanceRiskIndicator> indicators)
    {
        int activeCategoriesCount = indicators.Select(i => i.Category).Distinct().Count();
        if (activeCategoriesCount >= _options.CrossEvidenceMinCategoryCount)
        {
            var indicatorId = GenerateDeterministicIndicatorId(GovernanceRiskCategory.CrossEvidenceRisk, subjectId, $"CROSS-{activeCategoriesCount}");

            indicators.Add(new GovernanceRiskIndicator(
                indicatorId,
                GovernanceRiskCategory.CrossEvidenceRisk,
                _options.CrossEvidenceBonusWeight,
                DeriveSeverity(_options.CrossEvidenceBonusWeight),
                subjectId,
                Array.Empty<string>(),
                $"Compound risk active across {activeCategoriesCount} distinct governance risk categories.",
                "RULE-RISK-010 (Compound Cross-Evidence Risk Bonus)",
                $"Governance subject '{subjectId}' exhibits compound risk across {activeCategoriesCount} distinct governance risk categories.",
                GetRecommendedAction(_options.CrossEvidenceBonusWeight)
            ));
        }
    }

    public static GovernanceRiskSeverity DeriveSeverity(int score) => score switch
    {
        >= 75 => GovernanceRiskSeverity.Critical,
        >= 50 => GovernanceRiskSeverity.High,
        >= 25 => GovernanceRiskSeverity.Moderate,
        _     => GovernanceRiskSeverity.Low
    };

    private static string GetRecommendedAction(int scoreContribution)
    {
        return scoreContribution >= 20
            ? "Recommend priority human review by the Governance Audit Committee to verify procedural compliance and governance integrity."
            : "Recommend routine human review during periodic governance audit.";
    }

    private static bool IsOverrideType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        var t = type.Trim();
        return string.Equals(t, "Override", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(t, "Reversal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(t, "Supersede", StringComparison.OrdinalIgnoreCase);
    }

    private static List<DecisionHistoryObservation> NormalizeAndDeduplicateDecisions(IReadOnlyList<DecisionHistoryObservation> items)
    {
        if (items is null || items.Count == 0) return new List<DecisionHistoryObservation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DecisionHistoryObservation>();
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.DecisionId)) continue;
            var key = Normalize(item.DecisionId);
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }
        return result.OrderBy(d => d.Timestamp).ThenBy(d => Normalize(d.DecisionId), StringComparer.Ordinal).ToList();
    }

    private static List<ComplianceViolationEvidence> NormalizeAndDeduplicateViolations(IReadOnlyList<ComplianceViolationEvidence> items)
    {
        if (items is null || items.Count == 0) return new List<ComplianceViolationEvidence>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ComplianceViolationEvidence>();
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ViolationId)) continue;
            var key = Normalize(item.ViolationId);
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }
        return result.OrderBy(v => v.Timestamp).ThenBy(v => Normalize(v.ViolationId), StringComparer.Ordinal).ToList();
    }

    private static List<GovernanceConflictEvidence> NormalizeAndDeduplicateConflicts(IReadOnlyList<GovernanceConflictEvidence> items)
    {
        if (items is null || items.Count == 0) return new List<GovernanceConflictEvidence>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<GovernanceConflictEvidence>();
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ConflictId)) continue;
            var key = Normalize(item.ConflictId);
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }
        return result.OrderBy(c => c.Timestamp).ThenBy(c => Normalize(c.ConflictId), StringComparer.Ordinal).ToList();
    }

    private static List<ComplaintObservation> NormalizeAndDeduplicateComplaints(IReadOnlyList<ComplaintObservation> items)
    {
        if (items is null || items.Count == 0) return new List<ComplaintObservation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ComplaintObservation>();
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ComplaintId)) continue;
            var key = Normalize(item.ComplaintId);
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }
        return result.OrderBy(c => c.FilingTimestamp).ThenBy(c => Normalize(c.ComplaintId), StringComparer.Ordinal).ToList();
    }

    private static List<InstitutionalValidationObservation> NormalizeAndDeduplicateValidations(IReadOnlyList<InstitutionalValidationObservation> items)
    {
        if (items is null || items.Count == 0) return new List<InstitutionalValidationObservation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<InstitutionalValidationObservation>();
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ValidationId)) continue;
            var key = Normalize(item.ValidationId);
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }
        return result.OrderBy(v => v.Timestamp).ThenBy(v => Normalize(v.ValidationId), StringComparer.Ordinal).ToList();
    }

    private static string Normalize(string val)
    {
        return (val ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string GenerateDeterministicIndicatorId(GovernanceRiskCategory category, string subjectId, string evidenceKeys)
    {
        var raw = $"INDICATOR-{(int)category}-{subjectId}-{evidenceKeys}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var hashHex = Convert.ToHexString(bytes)[..8];
        return $"INDICATOR-{(int)category:D2}-{category}-{subjectId}-{hashHex}";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service implementation of the regulatory compliance engine.
/// </summary>
public sealed class RegulatoryComplianceEngine : IRegulatoryComplianceEngine
{
    public ComplianceResult Evaluate(LeaseEvaluationInput input, IEnumerable<RegulatoryRule> rules)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (rules is null) throw new ArgumentNullException(nameof(rules));

        var violations = new List<Violation>();
        var conditions = new List<ComplianceCondition>();

        foreach (var rule in rules.Where(r => r != null && r.IsActive))
        {
            rule.Evaluate(input, violations, conditions);
        }

        ComplianceStatus status;
        if (violations.Count > 0)
        {
            status = ComplianceStatus.NonCompliant;
        }
        else if (conditions.Count > 0)
        {
            status = ComplianceStatus.Conditional;
        }
        else
        {
            status = ComplianceStatus.Compliant;
        }

        var legacyFindings = violations
            .Select(v => new ComplianceFinding(
                v.RuleCode, "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
                RuleResultStatus.NonCompliant, "High", true, false, v.Message, "Legacy compliance requirement",
                EvidenceStatus.Missing, CalculationStatus.Calculated,
                new RuleSourceMetadata(RuleSourceType.ResearchConfiguration, "Legacy Research Prototype", "StateLandGovernance Prototype", "Legacy Rules", "N/A", "1.0"),
                "Address legacy compliance violation."))
            .Concat(conditions.Select(c => new ComplianceFinding(
                "LEGACY-COND", "1.0", RuleEvaluationType.Completeness, RuleApplicability.Applicable,
                RuleResultStatus.Conditional, "Low", false, false, c.Description, "Legacy condition requirement",
                EvidenceStatus.Provided, CalculationStatus.Calculated,
                new RuleSourceMetadata(RuleSourceType.ResearchConfiguration, "Legacy Research Prototype", "StateLandGovernance Prototype", "Legacy Rules", "N/A", "1.0"),
                "Fulfill legacy condition.")))
            .ToList();

        var deterministicId = GenerateDeterministicEvaluationId(input.ProposedUse, legacyFindings);

        return new ComplianceResult(status, legacyFindings, deterministicId, DateTime.UtcNow, violations, conditions);
    }

    public ComplianceResult EvaluateNpd(ProposalComplianceInput input, DateTime evaluationTimestamp)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        // 1. Evaluate all 24 NPD Operational Rules
        var findings = NpdRuleCatalogue.EvaluateAll(input);

        // 2. Determine Overall Compliance Status Precedence:
        //    a. NonCompliant ONLY if a finding is NonCompliant AND IsBlocking == true
        //    b. InsufficientInformation if required information prevents evaluation
        //    c. Conditional if conditions remain
        //    d. RequiresHumanReview if qualitative human review is required
        //    e. Compliant otherwise
        ComplianceStatus overallStatus;

        if (findings.Any(f => f.Status == RuleResultStatus.NonCompliant && f.IsBlocking))
        {
            overallStatus = ComplianceStatus.NonCompliant;
        }
        else if (findings.Any(f => f.Status == RuleResultStatus.InsufficientInformation))
        {
            overallStatus = ComplianceStatus.InsufficientInformation;
        }
        else if (findings.Any(f => f.Status == RuleResultStatus.Conditional))
        {
            overallStatus = ComplianceStatus.Conditional;
        }
        else if (findings.Any(f => f.Status == RuleResultStatus.RequiresHumanReview))
        {
            overallStatus = ComplianceStatus.RequiresHumanReview;
        }
        else if (findings.Any(f => f.Status == RuleResultStatus.NonCompliant))
        {
            // Non-blocking non-compliant findings fall to RequiresHumanReview / Conditional for operational review
            overallStatus = ComplianceStatus.RequiresHumanReview;
        }
        else
        {
            overallStatus = ComplianceStatus.Compliant;
        }

        // 3. Generate SHA-256 Deterministic Evaluation ID (Excludes raw EvaluationTimestamp)
        var deterministicId = GenerateDeterministicEvaluationId(input.ProposalId ?? "PROP-NPD", findings);

        return new ComplianceResult(overallStatus, findings, deterministicId, evaluationTimestamp);
    }

    private static string GenerateDeterministicEvaluationId(string seed, List<ComplianceFinding> findings)
    {
        var sortedRuleFindings = string.Join(";", findings
            .OrderBy(f => f.RuleCode, StringComparer.Ordinal)
            .Select(f => $"{f.RuleCode}:{f.RuleVersion}:{f.Status}:{f.ObservedValueSummary}"));

        var rawString = $"NPD-EVAL|{seed}|{sortedRuleFindings}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawString));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}

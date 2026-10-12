namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;

using System;
using System.Linq;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;

/// <summary>
/// Exhaustive deterministic translator from Component 4 ComplianceResultDto to Component 3 ScreeningOutcome.
/// 
/// Mapping Rules:
/// 1. Blocked:
///    - Status is 'NonCompliant'
///    - OR Violations.Count > 0
///    - OR Any finding has IsBlocking == true
///    - OR Any finding has Status == 'NonCompliant'
/// 
/// 2. Advisory:
///    - Not Blocked, AND:
///      - Status in ['Conditional', 'RequiresReview', 'RequiresHumanReview', 'InsufficientInformation']
///      - OR Conditions.Count > 0
///      - OR Any finding requires human review or is non-compliant/insufficient
/// 
/// 3. Cleared:
///    - Status is 'Compliant'
///    - AND Zero violations
///    - AND Zero conditions
///    - AND All findings (if any) are Compliant, non-blocking, and do not require human review.
/// 
/// 4. Defensive Fallback:
///    - Any unmapped or invalid status defaults to Blocked (fail-safe).
/// </summary>
public static class ComplianceResultTranslator
{
    public static (ScreeningOutcome Outcome, string Remarks) Translate(ComplianceResultDto? result)
    {
        if (result == null)
        {
            return (ScreeningOutcome.Blocked, "Component 4 returned a null compliance result. Evaluated as Blocked.");
        }

        var violations = result.Violations ?? Array.Empty<ViolationDto>();
        var conditions = result.Conditions ?? Array.Empty<ComplianceConditionDto>();
        var findings = result.Findings ?? Array.Empty<ComplianceFindingDto>();

        bool hasBlockingFinding = findings.Any(f => f.IsBlocking || string.Equals(f.Status, "NonCompliant", StringComparison.OrdinalIgnoreCase));
        bool hasViolations = violations.Count > 0;
        bool isNonCompliantStatus = string.Equals(result.Status, "NonCompliant", StringComparison.OrdinalIgnoreCase);

        // 1. Blocked Gate
        if (isNonCompliantStatus || hasViolations || hasBlockingFinding)
        {
            var remarks = BuildRemarks(ScreeningOutcome.Blocked, result, violations.Count, conditions.Count, findings.Count);
            return (ScreeningOutcome.Blocked, remarks);
        }

        // 2. Advisory Gate
        bool hasConditions = conditions.Count > 0;
        bool hasReviewFindings = findings.Any(f => f.RequiresHumanReview || !string.Equals(f.Status, "Compliant", StringComparison.OrdinalIgnoreCase));
        bool isAdvisoryStatus = string.Equals(result.Status, "Conditional", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(result.Status, "RequiresReview", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(result.Status, "RequiresHumanReview", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(result.Status, "InsufficientInformation", StringComparison.OrdinalIgnoreCase);

        if (isAdvisoryStatus || hasConditions || hasReviewFindings)
        {
            var remarks = BuildRemarks(ScreeningOutcome.Advisory, result, violations.Count, conditions.Count, findings.Count);
            return (ScreeningOutcome.Advisory, remarks);
        }

        // 3. Cleared Gate
        if (string.Equals(result.Status, "Compliant", StringComparison.OrdinalIgnoreCase))
        {
            var remarks = BuildRemarks(ScreeningOutcome.Cleared, result, 0, 0, findings.Count);
            return (ScreeningOutcome.Cleared, remarks);
        }

        // 4. Unknown Status Fallback
        return (ScreeningOutcome.Blocked, $"Unrecognized Component 4 status '{result.Status}'. Evaluated defensively as Blocked.");
    }

    private static string BuildRemarks(
        ScreeningOutcome outcome,
        ComplianceResultDto result,
        int violationCount,
        int conditionCount,
        int findingCount)
    {
        var sb = new StringBuilder();
        sb.Append($"Component 4 Assessment: {outcome}. Status={result.Status}");

        if (!string.IsNullOrWhiteSpace(result.DeterministicEvaluationId))
        {
            sb.Append($", EvaluationId={result.DeterministicEvaluationId}");
        }

        sb.Append($", Violations={violationCount}, Conditions={conditionCount}, Findings={findingCount}.");

        if (violationCount > 0 && result.Violations != null)
        {
            var violationSummary = string.Join("; ", result.Violations.Take(3).Select(v => $"[{v.RuleCode}] {v.Message}"));
            sb.Append($" Top Violations: {violationSummary}.");
        }

        if (conditionCount > 0 && result.Conditions != null)
        {
            var conditionSummary = string.Join("; ", result.Conditions.Take(3).Select(c => c.Description));
            sb.Append($" Key Conditions: {conditionSummary}.");
        }

        return sb.ToString();
    }
}


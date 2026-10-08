using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Deterministic domain engine performing conditional governance verification evaluations.
/// </summary>
public sealed class ConditionalGovernanceVerificationEngine : IConditionalGovernanceVerificationEngine
{
    public ConditionalVerificationResult EvaluateVerification(
        string subjectId,
        IEnumerable<GovernanceCondition> conditions,
        IEnumerable<VerificationEvidence> evidenceList,
        ConditionalVerificationPolicy policy,
        DateTime evaluationTimestamp)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("SubjectId cannot be null or empty.", nameof(subjectId));
        }

        if (conditions is null)
        {
            throw new ArgumentNullException(nameof(conditions), "Conditions collection cannot be null.");
        }

        var conditionList = conditions.ToList();
        if (conditionList.Count == 0)
        {
            throw new ArgumentException("Conditions collection cannot be empty.", nameof(conditions));
        }

        // Validate no duplicate ConditionId in input conditions
        var duplicateConditionIds = conditionList
            .GroupBy(c => c.ConditionId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateConditionIds.Count > 0)
        {
            throw new ArgumentException($"Duplicate condition ID detected: '{duplicateConditionIds.First()}'.", nameof(conditions));
        }

        var evidenceCollection = (evidenceList ?? Enumerable.Empty<VerificationEvidence>()).ToList();

        // Validate no duplicate evidence for same ConditionId
        var duplicateEvidenceIds = evidenceCollection
            .GroupBy(e => e.ConditionId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEvidenceIds.Count > 0)
        {
            throw new ArgumentException($"Duplicate evidence detected for condition ID: '{duplicateEvidenceIds.First()}'.", nameof(evidenceList));
        }

        // Map condition IDs for lookup
        var conditionIdMap = conditionList.ToDictionary(c => c.ConditionId, StringComparer.OrdinalIgnoreCase);

        // Validate unexpected evidence (evidence for unknown condition)
        foreach (var ev in evidenceCollection)
        {
            if (!conditionIdMap.ContainsKey(ev.ConditionId))
            {
                throw new ArgumentException($"Unexpected evidence provided for unknown condition ID: '{ev.ConditionId}'.", nameof(evidenceList));
            }
        }

        var effectivePolicy = policy ?? new ConditionalVerificationPolicy();
        var evidenceMap = evidenceCollection.ToDictionary(e => e.ConditionId, StringComparer.OrdinalIgnoreCase);

        var conditionStatuses = new List<ConditionVerificationStatus>();

        foreach (var condition in conditionList)
        {
            DerivedConditionStatus derivedStatus;
            string failureReason = string.Empty;
            string notes = string.Empty;

            if (!evidenceMap.TryGetValue(condition.ConditionId, out var evidence))
            {
                derivedStatus = DerivedConditionStatus.MissingEvidence;
                failureReason = "No evidence record provided for condition.";
            }
            else if (evidence.ProvidedStatus == SuppliedEvidenceStatus.Failed)
            {
                derivedStatus = DerivedConditionStatus.Failed;
                failureReason = "Submitted evidence is marked as failed/rejected.";
                notes = evidence.Remarks;
            }
            else if (evidence.ProvidedStatus == SuppliedEvidenceStatus.Pending)
            {
                derivedStatus = DerivedConditionStatus.Pending;
                failureReason = "Submitted evidence is currently pending review or issuance.";
                notes = evidence.Remarks;
            }
            else
            {
                // ProvidedStatus is Satisfied -> evaluate expiry rules
                bool isExpired = false;
                if (evidence.ExpiryTimestamp.HasValue && evidence.ExpiryTimestamp.Value <= evaluationTimestamp)
                {
                    isExpired = true;
                    failureReason = $"Evidence expired on explicit date {evidence.ExpiryTimestamp.Value:yyyy-MM-dd HH:mm:ss} UTC.";
                }
                else if (condition.MaxEvidenceAgeDays.HasValue &&
                         (evaluationTimestamp - evidence.EvidenceTimestamp).TotalDays > condition.MaxEvidenceAgeDays.Value)
                {
                    isExpired = true;
                    failureReason = $"Evidence age exceeds maximum allowable age of {condition.MaxEvidenceAgeDays.Value} days.";
                }

                if (isExpired)
                {
                    derivedStatus = DerivedConditionStatus.Expired;
                }
                else
                {
                    derivedStatus = DerivedConditionStatus.Satisfied;
                    notes = "Condition evidence is valid and satisfied.";
                }
            }

            conditionStatuses.Add(new ConditionVerificationStatus(
                conditionId: condition.ConditionId,
                isMandatory: condition.IsMandatory,
                status: derivedStatus,
                failureReason: failureReason,
                notes: notes));
        }

        // Aggregate Counts
        int totalConditionsCount = conditionStatuses.Count;
        int mandatoryConditionsCount = conditionStatuses.Count(c => c.IsMandatory);
        int satisfiedMandatoryCount = conditionStatuses.Count(c => c.IsMandatory && c.Status == DerivedConditionStatus.Satisfied);
        int unsatisfiedMandatoryCount = conditionStatuses.Count(c => c.IsMandatory && c.Status != DerivedConditionStatus.Satisfied);
        int satisfiedOptionalCount = conditionStatuses.Count(c => !c.IsMandatory && c.Status == DerivedConditionStatus.Satisfied);
        int pendingConditionsCount = conditionStatuses.Count(c => c.Status == DerivedConditionStatus.Pending);
        int missingConditionsCount = conditionStatuses.Count(c => c.Status == DerivedConditionStatus.MissingEvidence);
        int expiredConditionsCount = conditionStatuses.Count(c => c.Status == DerivedConditionStatus.Expired);

        // Determine Overall Outcome
        ConditionalVerificationOutcome outcome;

        bool hasMandatoryHardFailure = conditionStatuses.Any(c => c.IsMandatory && (c.Status == DerivedConditionStatus.Failed || c.Status == DerivedConditionStatus.Expired));
        bool hasMandatoryIncomplete = conditionStatuses.Any(c => c.IsMandatory && (c.Status == DerivedConditionStatus.MissingEvidence || c.Status == DerivedConditionStatus.Pending));

        if (hasMandatoryHardFailure)
        {
            outcome = ConditionalVerificationOutcome.Unsatisfied;
        }
        else if (hasMandatoryIncomplete)
        {
            outcome = ConditionalVerificationOutcome.PendingEvidence;
        }
        else
        {
            // All mandatory conditions are Satisfied
            bool allOptionalSatisfied = conditionStatuses.Where(c => !c.IsMandatory).All(c => c.Status == DerivedConditionStatus.Satisfied);

            if (allOptionalSatisfied)
            {
                outcome = ConditionalVerificationOutcome.FullySatisfied;
            }
            else if (effectivePolicy.AllowProvisionalVerification)
            {
                outcome = ConditionalVerificationOutcome.ProvisionallySatisfied;
            }
            else
            {
                outcome = ConditionalVerificationOutcome.Unsatisfied;
            }
        }

        // Compute Deterministic Hashing ID
        string verificationId = GenerateDeterministicId(
            normalizedSubjectId: subjectId.Trim().ToLowerInvariant(),
            conditions: conditionList,
            derivedStatuses: conditionStatuses,
            policy: effectivePolicy);

        // Generate Explainable Summary & Action Recommendation
        string summaryExplanation = BuildSummaryExplanation(outcome, totalConditionsCount, satisfiedMandatoryCount, mandatoryConditionsCount, unsatisfiedMandatoryCount);
        string recommendedAction = BuildRecommendedAction(outcome);

        return new ConditionalVerificationResult(
            verificationId: verificationId,
            subjectId: subjectId.Trim(),
            outcome: outcome,
            totalConditionsCount: totalConditionsCount,
            mandatoryConditionsCount: mandatoryConditionsCount,
            satisfiedMandatoryCount: satisfiedMandatoryCount,
            unsatisfiedMandatoryCount: unsatisfiedMandatoryCount,
            satisfiedOptionalCount: satisfiedOptionalCount,
            pendingConditionsCount: pendingConditionsCount,
            missingConditionsCount: missingConditionsCount,
            expiredConditionsCount: expiredConditionsCount,
            conditionStatuses: conditionStatuses,
            summaryExplanation: summaryExplanation,
            recommendedAction: recommendedAction,
            evaluationTimestamp: evaluationTimestamp);
    }

    private static string GenerateDeterministicId(
        string normalizedSubjectId,
        List<GovernanceCondition> conditions,
        List<ConditionVerificationStatus> derivedStatuses,
        ConditionalVerificationPolicy policy)
    {
        var sb = new StringBuilder();
        sb.Append("Subject:").Append(normalizedSubjectId).Append(';');

        // Sort conditions case-insensitively by ConditionId
        var sortedConditions = conditions.OrderBy(c => c.ConditionId, StringComparer.OrdinalIgnoreCase).ToList();
        sb.Append("Conditions:");
        foreach (var c in sortedConditions)
        {
            sb.Append(c.ConditionId.Trim().ToLowerInvariant())
              .Append(',')
              .Append(c.IsMandatory ? '1' : '0')
              .Append(',')
              .Append(c.MaxEvidenceAgeDays?.ToString() ?? "null")
              .Append('|');
        }
        sb.Append(';');

        // Sort derived statuses case-insensitively by ConditionId
        var sortedStatuses = derivedStatuses.OrderBy(s => s.ConditionId, StringComparer.OrdinalIgnoreCase).ToList();
        sb.Append("Statuses:");
        foreach (var s in sortedStatuses)
        {
            sb.Append(s.ConditionId.Trim().ToLowerInvariant())
              .Append(':')
              .Append((int)s.Status)
              .Append('|');
        }
        sb.Append(';');

        sb.Append("Policy:").Append(policy.AllowProvisionalVerification ? '1' : '0');

        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        string hexHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"COND-VERIF-{hexHash[..16]}";
    }

    private static string BuildSummaryExplanation(
        ConditionalVerificationOutcome outcome,
        int total,
        int satisfiedMandatory,
        int mandatoryCount,
        int unsatisfiedMandatory)
    {
        return outcome switch
        {
            ConditionalVerificationOutcome.FullySatisfied =>
                $"Governance verification fully satisfied. All {total} prerequisite governance conditions evaluated as satisfied.",
            ConditionalVerificationOutcome.ProvisionallySatisfied =>
                $"Governance verification provisionally satisfied. All {satisfiedMandatory} of {mandatoryCount} mandatory conditions are satisfied, with open optional conditions.",
            ConditionalVerificationOutcome.PendingEvidence =>
                $"Governance verification pending evidence. {unsatisfiedMandatory} mandatory condition(s) are missing or pending evidence without hard failure.",
            ConditionalVerificationOutcome.Unsatisfied =>
                $"Governance verification unsatisfied. Mandatory condition(s) failed or expired.",
            _ => "Governance condition verification evaluated."
        };
    }

    private static string BuildRecommendedAction(ConditionalVerificationOutcome outcome)
    {
        return outcome switch
        {
            ConditionalVerificationOutcome.FullySatisfied =>
                "Proceed to subsequent governance decision workflows.",
            ConditionalVerificationOutcome.ProvisionallySatisfied =>
                "Human review recommended to verify whether optional prerequisite conditions should be fulfilled prior to final decision.",
            ConditionalVerificationOutcome.PendingEvidence =>
                "Human review recommended to request missing or pending prerequisite evidence from issuing authorities.",
            ConditionalVerificationOutcome.Unsatisfied =>
                "Human review recommended to address mandatory condition failures or request renewed clearances.",
            _ => "Human review recommended."
        };
    }
}

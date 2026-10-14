using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

public sealed class GovernanceConsensusEngine : IGovernanceConsensusEngine
{
    public GovernanceConsensusResult EvaluateConsensus(
        string subjectId,
        IEnumerable<InstitutionalGovernancePosition> positions,
        GovernanceConsensusPolicy policy,
        DateTime evaluationTimestamp)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("Subject ID cannot be null or empty.", nameof(subjectId));
        }

        if (policy is null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        var normalizedSubjectId = subjectId.Trim().ToUpperInvariant();
        var positionList = (positions ?? Array.Empty<InstitutionalGovernancePosition>()).ToList();

        // 1. Check duplicate institution IDs in position list
        var duplicate = positionList
            .GroupBy(p => p.InstitutionId)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate != null)
        {
            throw new ArgumentException($"Duplicate position submission for institution: '{duplicate.Key}'.", nameof(positions));
        }

        // 2. Check unexpected institution IDs
        var expectedSet = new HashSet<string>(policy.ExpectedInstitutionIds);
        var unexpected = positionList.FirstOrDefault(p => !expectedSet.Contains(p.InstitutionId));
        if (unexpected != null)
        {
            throw new ArgumentException($"Position submitted for unexpected institution: '{unexpected.InstitutionId}'.", nameof(positions));
        }

        // 3. Count categories
        int totalExpected = policy.ExpectedInstitutionIds.Count;
        int submittedCount = positionList.Count;
        int missingCount = totalExpected - submittedCount;

        int approvalCount = positionList.Count(p => p.Position == InstitutionalPositionType.Approve);
        int rejectionCount = positionList.Count(p => p.Position == InstitutionalPositionType.Reject);
        int conditionalCount = positionList.Count(p => p.Position == InstitutionalPositionType.ConditionalApprove);
        int abstentionCount = positionList.Count(p => p.Position == InstitutionalPositionType.Abstain);
        int pendingCount = positionList.Count(p => p.Position == InstitutionalPositionType.Pending);

        int effectiveApprovals = policy.AllowConditionalAsApproval
            ? approvalCount + conditionalCount
            : approvalCount;

        int participatingCount = policy.CountAbstentionsInQuorum
            ? approvalCount + rejectionCount + conditionalCount + abstentionCount
            : approvalCount + rejectionCount + conditionalCount;

        double quorumPercentage = (participatingCount / (double)totalExpected) * 100.0;
        bool quorumSatisfied = quorumPercentage >= policy.MinQuorumPercentage;

        // 4. Mandatory Institutions evaluation
        var mandatorySet = new HashSet<string>(policy.MandatoryInstitutionIds);
        var positionMap = positionList.ToDictionary(p => p.InstitutionId);

        bool mandatorySatisfied = true;
        int mandatoryRejections = 0;
        int mandatoryMissingOrPending = 0;

        foreach (var mandatoryId in policy.MandatoryInstitutionIds)
        {
            if (!positionMap.TryGetValue(mandatoryId, out var posRecord))
            {
                mandatorySatisfied = false;
                mandatoryMissingOrPending++;
            }
            else if (posRecord.Position == InstitutionalPositionType.Reject)
            {
                mandatorySatisfied = false;
                mandatoryRejections++;
            }
            else if (posRecord.Position == InstitutionalPositionType.Pending)
            {
                mandatorySatisfied = false;
                mandatoryMissingOrPending++;
            }
            else if (posRecord.Position == InstitutionalPositionType.Abstain)
            {
                mandatorySatisfied = false;
            }
            else if (posRecord.Position == InstitutionalPositionType.ConditionalApprove && !policy.AllowConditionalAsApproval)
            {
                // Mandatory institution approved conditionally, but policy does not count conditional as full approval
                // Still satisfies participation, but requires conditional outcome
            }
        }

        // 5. Rejection & Blocking Count
        int blockingCount = mandatoryRejections;
        if (policy.IsRejectionBlocking)
        {
            int nonMandatoryRejections = positionList.Count(p => p.Position == InstitutionalPositionType.Reject && !mandatorySet.Contains(p.InstitutionId));
            blockingCount += nonMandatoryRejections;
        }

        // 6. Threshold Satisfaction
        bool consensusThresholdSatisfied = false;

        switch (policy.Mode)
        {
            case ConsensusType.Unanimous:
                consensusThresholdSatisfied = (effectiveApprovals == totalExpected) && (missingCount == 0) && (pendingCount == 0) && (rejectionCount == 0);
                break;

            case ConsensusType.SimpleMajority:
                consensusThresholdSatisfied = ((effectiveApprovals / (double)totalExpected) * 100.0) > 50.0;
                break;

            case ConsensusType.Supermajority:
                consensusThresholdSatisfied = ((effectiveApprovals / (double)totalExpected) * 100.0) >= policy.RequiredPercentage;
                break;

            case ConsensusType.ThresholdCount:
                consensusThresholdSatisfied = effectiveApprovals >= (policy.RequiredApprovalCount ?? totalExpected);
                break;
        }

        // 7. Outcome Determination
        ConsensusOutcome outcome;

        if (blockingCount > 0)
        {
            outcome = ConsensusOutcome.Blocked;
        }
        else if (!quorumSatisfied)
        {
            outcome = ConsensusOutcome.InsufficientQuorum;
        }
        else if (!mandatorySatisfied)
        {
            if (mandatoryMissingOrPending > 0)
            {
                outcome = ConsensusOutcome.Pending;
            }
            else
            {
                outcome = ConsensusOutcome.ConsensusNotReached;
            }
        }
        else if (!consensusThresholdSatisfied)
        {
            if (missingCount > 0 || pendingCount > 0)
            {
                outcome = ConsensusOutcome.Pending;
            }
            else
            {
                outcome = ConsensusOutcome.ConsensusNotReached;
            }
        }
        else
        {
            // All thresholds, quorum, and mandatory constraints met
            if (conditionalCount > 0)
            {
                outcome = ConsensusOutcome.ConditionalConsensus;
            }
            else
            {
                outcome = ConsensusOutcome.ConsensusReached;
            }
        }

        // 8. Plain Language Explanations & Recommendations
        string summaryExplanation = BuildSummaryExplanation(outcome, totalExpected, submittedCount, participatingCount, missingCount, approvalCount, rejectionCount, conditionalCount, abstentionCount, pendingCount, policy);
        string recommendedAction = BuildRecommendedAction(outcome);

        // 9. Deterministic Identity
        string evaluationId = ComputeDeterministicEvaluationId(normalizedSubjectId, policy, positionList);

        return new GovernanceConsensusResult(
            ConsensusEvaluationId: evaluationId,
            SubjectId: normalizedSubjectId,
            Outcome: outcome,
            TotalExpectedInstitutions: totalExpected,
            SubmittedCount: submittedCount,
            ParticipatingCount: participatingCount,
            MissingCount: missingCount,
            ApprovalCount: approvalCount,
            RejectionCount: rejectionCount,
            ConditionalApprovalCount: conditionalCount,
            AbstentionCount: abstentionCount,
            PendingCount: pendingCount,
            QuorumSatisfied: quorumSatisfied,
            MandatoryInstitutionsSatisfied: mandatorySatisfied,
            ConsensusThresholdSatisfied: consensusThresholdSatisfied,
            BlockingInstitutionCount: blockingCount,
            SummaryExplanation: summaryExplanation,
            RecommendedAction: recommendedAction,
            EvaluationTimestamp: evaluationTimestamp
        );
    }

    private static string BuildSummaryExplanation(
        ConsensusOutcome outcome,
        int totalExpected,
        int submittedCount,
        int participatingCount,
        int missingCount,
        int approvalCount,
        int rejectionCount,
        int conditionalCount,
        int abstentionCount,
        int pendingCount,
        GovernanceConsensusPolicy policy)
    {
        return outcome switch
        {
            ConsensusOutcome.ConsensusReached =>
                $"Consensus successfully reached across {totalExpected} expected institutions ({approvalCount} approvals, {conditionalCount} conditional, {rejectionCount} rejections, {abstentionCount} abstentions). Mode: {policy.Mode}.",
            ConsensusOutcome.ConditionalConsensus =>
                $"Consensus threshold satisfied across {totalExpected} expected institutions, subject to {conditionalCount} conditional approval requirement(s). Mode: {policy.Mode}.",
            ConsensusOutcome.Blocked =>
                $"Governance consensus is blocked due to {rejectionCount} rejection(s) from blocking/mandatory institution(s).",
            ConsensusOutcome.InsufficientQuorum =>
                $"Insufficient quorum participation: {participatingCount} of {totalExpected} expected institutions participated ({((participatingCount / (double)totalExpected) * 100.0):F1}%), below required quorum of {policy.MinQuorumPercentage:F1}%.",
            ConsensusOutcome.Pending =>
                $"Governance consensus evaluation is pending: {missingCount} institution(s) missing submissions and {pendingCount} institution(s) pending review.",
            _ =>
                $"Consensus threshold not met: {approvalCount} approval(s) submitted out of {totalExpected} expected institution(s) under {policy.Mode} policy."
        };
    }

    private static string BuildRecommendedAction(ConsensusOutcome outcome)
    {
        return outcome switch
        {
            ConsensusOutcome.ConsensusReached =>
                "Proceed to governance review and documentation.",
            ConsensusOutcome.ConditionalConsensus =>
                "Review and verify specified institutional conditions before proceeding.",
            ConsensusOutcome.Blocked =>
                "Escalate to governance dispute resolution committee to address institutional rejections.",
            ConsensusOutcome.InsufficientQuorum =>
                "Await additional institutional submissions to satisfy minimum quorum requirements.",
            ConsensusOutcome.Pending =>
                "Notify pending and missing institutions to submit formal governance positions.",
            _ =>
                "Submit consensus findings to inter-agency coordination board for manual review."
        };
    }

    private static string ComputeDeterministicEvaluationId(
        string normalizedSubjectId,
        GovernanceConsensusPolicy policy,
        List<InstitutionalGovernancePosition> positions)
    {
        var sortedExpectedIds = string.Join(",", policy.ExpectedInstitutionIds.OrderBy(id => id, StringComparer.Ordinal));
        var sortedMandatoryIds = string.Join(",", policy.MandatoryInstitutionIds.OrderBy(id => id, StringComparer.Ordinal));
        
        var sortedPositions = string.Join(";", positions
            .OrderBy(p => p.InstitutionId, StringComparer.Ordinal)
            .Select(p => $"{p.InstitutionId}:{(int)p.Position}:{(p.ReasonCode ?? "").Trim().ToUpperInvariant()}"));

        string rawText = $"{normalizedSubjectId}|{(int)policy.Mode}|{policy.RequiredPercentage:F4}|{policy.RequiredApprovalCount?.ToString() ?? "NONE"}|{policy.MinQuorumPercentage:F4}|{policy.AllowConditionalAsApproval}|{policy.CountAbstentionsInQuorum}|{policy.IsRejectionBlocking}|{sortedExpectedIds}|{sortedMandatoryIds}|{sortedPositions}";

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawText));
        var hash16 = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16).ToLowerInvariant();

        return $"CNS-EVAL-{hash16}";
    }
}

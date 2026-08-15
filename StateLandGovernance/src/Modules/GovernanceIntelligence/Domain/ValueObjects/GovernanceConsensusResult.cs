using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

public sealed record GovernanceConsensusResult(
    string ConsensusEvaluationId,
    string SubjectId,
    ConsensusOutcome Outcome,
    int TotalExpectedInstitutions,
    int SubmittedCount,
    int ParticipatingCount,
    int MissingCount,
    int ApprovalCount,
    int RejectionCount,
    int ConditionalApprovalCount,
    int AbstentionCount,
    int PendingCount,
    bool QuorumSatisfied,
    bool MandatoryInstitutionsSatisfied,
    bool ConsensusThresholdSatisfied,
    int BlockingInstitutionCount,
    string SummaryExplanation,
    string RecommendedAction,
    DateTime EvaluationTimestamp
);

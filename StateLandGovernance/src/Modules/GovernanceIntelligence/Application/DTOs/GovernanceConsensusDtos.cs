using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public sealed record InstitutionalGovernancePositionDto(
    string InstitutionId,
    string Position,
    string? AuthorityRole = null,
    string? ReasonCode = null,
    string? SummaryNotes = null,
    DateTime? SubmittedTimestamp = null
);

public sealed record GovernanceConsensusPolicyDto(
    string Mode,
    List<string> ExpectedInstitutionIds,
    double RequiredPercentage = 50.0,
    int? RequiredApprovalCount = null,
    double MinQuorumPercentage = 50.0,
    bool AllowConditionalAsApproval = false,
    bool CountAbstentionsInQuorum = true,
    bool IsRejectionBlocking = false,
    List<string>? MandatoryInstitutionIds = null
);

public sealed record GovernanceConsensusInputDto(
    string SubjectId,
    List<InstitutionalGovernancePositionDto> Positions,
    GovernanceConsensusPolicyDto Policy
);

public sealed record GovernanceConsensusResultDto(
    string ConsensusEvaluationId,
    string SubjectId,
    string Outcome,
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

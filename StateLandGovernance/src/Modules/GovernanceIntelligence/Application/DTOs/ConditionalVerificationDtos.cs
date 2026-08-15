using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public sealed record GovernanceConditionDto(
    string ConditionId,
    string Title = "",
    string Category = "",
    bool IsMandatory = true,
    int? MaxEvidenceAgeDays = null,
    string Description = ""
);

public sealed record VerificationEvidenceDto(
    string ConditionId,
    string EvidenceId = "",
    string ProvidedStatus = "Satisfied",
    DateTime? EvidenceTimestamp = null,
    DateTime? ExpiryTimestamp = null,
    string IssuerOrAuthority = "",
    string Remarks = ""
);

public sealed record ConditionalVerificationPolicyDto(
    bool AllowProvisionalVerification = true
);

public sealed record ConditionVerificationStatusDto(
    string ConditionId,
    bool IsMandatory,
    string Status,
    bool IsSatisfied,
    string FailureReason,
    string Notes
);

public sealed record ConditionalVerificationInputDto(
    string SubjectId,
    List<GovernanceConditionDto> Conditions,
    List<VerificationEvidenceDto>? Evidence = null,
    ConditionalVerificationPolicyDto? Policy = null
);

public sealed record ConditionalVerificationResultDto(
    string VerificationId,
    string SubjectId,
    string Outcome,
    int TotalConditionsCount,
    int MandatoryConditionsCount,
    int SatisfiedMandatoryCount,
    int UnsatisfiedMandatoryCount,
    int SatisfiedOptionalCount,
    int PendingConditionsCount,
    int MissingConditionsCount,
    int ExpiredConditionsCount,
    List<ConditionVerificationStatusDto> ConditionStatuses,
    string SummaryExplanation,
    string RecommendedAction,
    DateTime EvaluationTimestamp
);

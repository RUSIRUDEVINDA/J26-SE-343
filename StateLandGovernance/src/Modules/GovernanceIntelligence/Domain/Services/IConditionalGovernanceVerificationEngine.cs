using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service interface for performing deterministic conditional governance verification.
/// </summary>
public interface IConditionalGovernanceVerificationEngine
{
    /// <summary>
    /// Evaluates prerequisite governance conditions against supplied evidence and policy settings.
    /// </summary>
    ConditionalVerificationResult EvaluateVerification(
        string subjectId,
        IEnumerable<GovernanceCondition> conditions,
        IEnumerable<VerificationEvidence> evidenceList,
        ConditionalVerificationPolicy policy,
        DateTime evaluationTimestamp);
}

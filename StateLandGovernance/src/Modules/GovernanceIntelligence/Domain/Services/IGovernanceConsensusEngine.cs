using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

public interface IGovernanceConsensusEngine
{
    GovernanceConsensusResult EvaluateConsensus(
        string subjectId,
        IEnumerable<InstitutionalGovernancePosition> positions,
        GovernanceConsensusPolicy policy,
        DateTime evaluationTimestamp);
}

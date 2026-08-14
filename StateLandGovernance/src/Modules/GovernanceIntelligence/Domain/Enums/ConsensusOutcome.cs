using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

public enum ConsensusOutcome
{
    ConsensusReached = 1,     // All policy conditions met (threshold, quorum, mandatory)
    ConditionalConsensus = 2, // Threshold met, but contains ConditionalApprove
    ConsensusNotReached = 3,  // Threshold not met (without explicit veto)
    Blocked = 4,              // Blocked by explicit mandatory rejection or opt-in rejection blocking
    InsufficientQuorum = 5,   // Participation below minimum quorum percentage
    Pending = 6               // Required institutions missing or review pending
}

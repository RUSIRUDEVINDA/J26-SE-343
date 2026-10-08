using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

public enum ConsensusType
{
    Unanimous = 1,       // 100% approval required
    SimpleMajority = 2,  // > 50% approval required
    Supermajority = 3,   // Configurable threshold (e.g. 66.67%, 75%)
    ThresholdCount = 4   // Fixed minimum number of approvals (e.g. 3 approvals)
}

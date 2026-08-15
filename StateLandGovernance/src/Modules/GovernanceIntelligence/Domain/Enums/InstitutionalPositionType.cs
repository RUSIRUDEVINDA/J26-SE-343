using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

public enum InstitutionalPositionType
{
    Approve = 1,            // Explicit unconditional approval
    Reject = 2,             // Explicit rejection / disapproval
    ConditionalApprove = 3, // Approval subject to specified conditions
    Abstain = 4,            // Formal decision to abstain from voting
    Pending = 5             // Review in progress / decision pending
}

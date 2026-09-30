namespace StateLandGovernance.WorkflowGovernance.Domain.Tracking;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record EscalationRequest(
    Guid Id,
    LeaseCaseId LeaseCaseId,
    Guid TaskId,
    string Reason,
    DateTime RequestedAtUtc);

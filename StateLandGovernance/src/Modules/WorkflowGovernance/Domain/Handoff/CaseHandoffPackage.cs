namespace StateLandGovernance.WorkflowGovernance.Domain.Handoff;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record CaseHandoffPackage(
    Guid Id,
    LeaseCaseId LeaseCaseId,
    Guid FinalWorkflowPlanId,
    Guid VerifiedFactSnapshotId,
    DateTime GeneratedAtUtc,
    Guid? DownstreamAcknowledgementId)
{
    public CaseHandoffPackage WithAcknowledgement(Guid trackingId) =>
        this with { DownstreamAcknowledgementId = trackingId };
}

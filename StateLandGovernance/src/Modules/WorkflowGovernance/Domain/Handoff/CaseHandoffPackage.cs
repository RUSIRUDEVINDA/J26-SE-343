namespace StateLandGovernance.WorkflowGovernance.Domain.Handoff;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record CaseHandoffPackage(
    Guid Id,
    LeaseCaseId LeaseCaseId,
    Guid FinalWorkflowPlanId,
    Guid VerifiedFactSnapshotId,
    DateTime GeneratedAtUtc,
    Guid? DownstreamAcknowledgementId,
    Guid? FinalDecisionId = null,
    IReadOnlyList<GovernedDocumentId>? FulfilledDocumentIds = null)
{
    public CaseHandoffPackage WithAcknowledgement(Guid trackingId) =>
        this with { DownstreamAcknowledgementId = trackingId };
}

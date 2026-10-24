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
    IReadOnlyList<GovernedDocumentId>? FulfilledDocumentIds = null,
    string? RecipientContract = null,
    string? IdempotencyKey = null,
    IReadOnlyList<HandoffDispatchReceipt>? DispatchHistory = null,
    DateTime? AcknowledgedAtUtc = null,
    Guid? AcknowledgedByActorId = null)
{
    public CaseHandoffPackage WithAcknowledgement(Guid trackingId) =>
        this with { DownstreamAcknowledgementId = trackingId };
}

public sealed record HandoffDispatchReceipt(Guid AttemptId, DateTime AttemptedAtUtc, Guid ActorId,
    bool AcceptedByTransport, string EvidenceReference);

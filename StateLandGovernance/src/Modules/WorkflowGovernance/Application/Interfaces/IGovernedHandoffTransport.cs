namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed record HandoffTransportContract(string Identifier, string Version);
public sealed record HandoffTransportRequest(Guid PackageId, Guid CaseId, Guid PlanId, Guid SnapshotId,
    Guid FinalDecisionId, string RecipientContract, string IdempotencyKey, IReadOnlyList<Guid> FulfilledDocumentIds);
public sealed record HandoffTransportResult(bool Accepted, string EvidenceReference, Guid? AcknowledgementId = null);

/// <summary>Trusted recipient adapter. Applicant input cannot establish delivery or acknowledgement.</summary>
public interface IGovernedHandoffTransport
{
    Task<HandoffTransportContract?> ReadContractAsync(CancellationToken ct);
    Task<HandoffTransportResult> DispatchAsync(HandoffTransportRequest request, CancellationToken ct);
}

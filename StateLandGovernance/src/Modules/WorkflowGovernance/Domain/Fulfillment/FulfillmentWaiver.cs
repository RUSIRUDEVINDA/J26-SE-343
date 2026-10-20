namespace StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;

using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record FulfillmentWaiver(Guid ActorId, string Reason, string EvidenceReference,
    DateTime RecordedAtUtc, FulfillmentStatus PreviousStatus)
{
    internal static FulfillmentWaiver Create(LeaseCaseId caseId, FulfillmentStatus status,
        VerifiedAuthoritySnapshot authority, string reason, string evidenceReference, DateTime at)
    {
        if (authority == null) throw new MissingVerifiedAuthorityException("Waiver authority is required.");
        authority.EnsureAuthorizes(authority.ActorId, "FulfillmentWaiver",
            new(AuthorityScopeKind.LeaseCase, caseId.Value.ToString("D")), at);
        if (status is not (FulfillmentStatus.Pending or FulfillmentStatus.Overdue))
            throw new InvalidFulfillmentException("Only pending or overdue obligations may be waived.");
        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(evidenceReference))
            throw new InvalidFulfillmentException("A waiver requires a reason and source evidence.");
        return new(authority.ActorId, reason.Trim(), evidenceReference.Trim(), at, status);
    }
}

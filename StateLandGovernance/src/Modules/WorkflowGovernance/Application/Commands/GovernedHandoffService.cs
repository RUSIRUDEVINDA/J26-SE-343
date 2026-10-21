namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;

public sealed record HandoffPackageDto(Guid Id, Guid CaseId, Guid PlanId, Guid SnapshotId, Guid? FinalDecisionId,
    DateTime PreparedAtUtc, string? RecipientContract, string? IdempotencyKey, Guid? AcknowledgementId,
    DateTime? AcknowledgedAtUtc, IReadOnlyList<HandoffDispatchReceiptDto> DispatchHistory);
public sealed record HandoffDispatchReceiptDto(Guid AttemptId, DateTime AttemptedAtUtc, Guid ActorId,
    bool AcceptedByTransport, string EvidenceReference);
public sealed record GovernedHandoffState(Guid CaseId, int CaseRevision, bool CanPrepare, bool CanDispatch,
    IReadOnlyList<string> MissingInputs, HandoffPackageDto? Package);
public sealed record PrepareHandoffRequest(int ExpectedCaseRevision, Guid PackageId, string IdempotencyKey);
public sealed record DispatchHandoffRequest(int ExpectedCaseRevision, Guid PackageId);

public sealed class GovernedHandoffService(ILeaseCaseRepository cases, ProjectFulfillmentService fulfillment,
    IGovernedHandoffTransport transport, IWorkflowGovernanceUnitOfWork unit, TimeProvider clock)
{
    public async Task<GovernedHandoffState> GetAsync(Guid caseId, CancellationToken ct)
    {
        var c = await cases.GetByIdAsync(new(caseId), ct) ?? throw new InvalidOperationException("Case not found.");
        var state = await fulfillment.GetAsync(caseId, ct);
        var missing = state.MissingInputs.ToList();
        if (!state.ReadyForHandoff) missing.Add("Completed final record and all current fulfilled or governed-waived obligations are required.");
        var contract = await transport.ReadContractAsync(ct);
        if (contract == null) missing.Add("Actual downstream recipient contract/version and authenticated transport are not configured.");
        var p = c.HandoffPackage;
        if (p != null && contract != null && p.RecipientContract != contract.Identifier + ":" + contract.Version)
            missing.Add("Prepared package recipient contract differs from the active transport contract.");
        return new(caseId, c.Revision, missing.Count == 0 && p == null, missing.Count == 0 && p != null && !p.DownstreamAcknowledgementId.HasValue,
            missing, p == null ? null : new(p.Id, p.LeaseCaseId.Value, p.FinalWorkflowPlanId, p.VerifiedFactSnapshotId,
                p.FinalDecisionId, p.GeneratedAtUtc, p.RecipientContract, p.IdempotencyKey, p.DownstreamAcknowledgementId,
                p.AcknowledgedAtUtc, p.DispatchHistory?.Select(d => new HandoffDispatchReceiptDto(d.AttemptId,
                    d.AttemptedAtUtc, d.ActorId, d.AcceptedByTransport, d.EvidenceReference)).ToArray() ?? Array.Empty<HandoffDispatchReceiptDto>()));
    }

    public async Task<GovernedHandoffState> PrepareAsync(Guid caseId, PrepareHandoffRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        var state = await GetAsync(caseId, ct);
        if (!state.CanPrepare || state.CaseRevision != request.ExpectedCaseRevision) throw new InvalidOperationException(string.Join(" ", state.MissingInputs.Append("Preparation state/revision must be current.")));
        var contract = await transport.ReadContractAsync(ct) ?? throw new InvalidOperationException("Recipient is unavailable.");
        var c = (await cases.GetByIdAsync(new(caseId), ct))!;
        c.PrepareGovernedHandoff(request.ExpectedCaseRevision, actor.ToDomain(), contract.Identifier + ":" + contract.Version,
            request.IdempotencyKey, clock.GetUtcNow().UtcDateTime, request.PackageId);
        await unit.CommitAsync(ct);
        return await GetAsync(caseId, ct);
    }

    public async Task<GovernedHandoffState> DispatchAsync(Guid caseId, DispatchHandoffRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        var at = clock.GetUtcNow().UtcDateTime;
        var authority = actor.ToDomain();
        authority.EnsureAuthorizes(actor.ActorId, "HandoffDispatcher", new(Domain.Authority.AuthorityScopeKind.LeaseCase, caseId.ToString("D")), at);
        authority.EnsureAuthorizes(actor.ActorId, "HandoffAcknowledger", new(Domain.Authority.AuthorityScopeKind.LeaseCase, caseId.ToString("D")), at);
        var state = await GetAsync(caseId, ct);
        if (!state.CanDispatch || state.CaseRevision != request.ExpectedCaseRevision || state.Package?.Id != request.PackageId)
            throw new InvalidOperationException("Handoff prerequisites, package or revision are stale/incomplete.");
        var c = (await cases.GetByIdAsync(new(caseId), ct))!; var p = c.HandoffPackage!;
        var result = await transport.DispatchAsync(new(p.Id, caseId, p.FinalWorkflowPlanId, p.VerifiedFactSnapshotId,
            p.FinalDecisionId!.Value, p.RecipientContract!, p.IdempotencyKey!, p.FulfilledDocumentIds?.Select(d => d.Value).ToArray() ?? Array.Empty<Guid>()), ct);
        if (string.IsNullOrWhiteSpace(result.EvidenceReference) || (!result.Accepted && result.AcknowledgementId.HasValue))
            throw new InvalidOperationException("Transport response is invalid; no acknowledgement was recorded.");
        at = clock.GetUtcNow().UtcDateTime;
        c.RecordGovernedHandoffDispatch(p.Id, request.ExpectedCaseRevision, authority, Guid.NewGuid(), result.Accepted, result.EvidenceReference, at);
        if (result.AcknowledgementId.HasValue)
            c.AcknowledgeGovernedHandoff(p.Id, p.RecipientContract!, p.IdempotencyKey!, p.FinalWorkflowPlanId, p.VerifiedFactSnapshotId,
                p.FinalDecisionId.Value, result.AcknowledgementId.Value, c.Revision, authority, at);
        await unit.CommitAsync(ct);
        return await GetAsync(caseId, ct);
    }
}

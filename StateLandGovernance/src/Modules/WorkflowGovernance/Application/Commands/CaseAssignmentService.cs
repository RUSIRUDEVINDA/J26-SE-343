namespace StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

/// <summary>Intake responsibility only; never verifies facts or approves an application.</summary>
public sealed class CaseAssignmentService(ICaseAssignmentStore store, IIntakeAssignmentDirectory directory)
{
    public static readonly string[] ProposalReviewCapabilities = ["CaseWorkspaceReviewer", "FactVerifier",
        "FactSnapshotPublisher", "ProposalReviewer", "ClassificationReviewer", "HumanEvidenceReviewer"];
    public static IntakeJurisdictionPolicy? ResolveJurisdiction(IEnumerable<IntakeJurisdictionPolicy> policies,
        string? declaredJurisdiction, DateTimeOffset intakeTime)
    {
        if (string.IsNullOrWhiteSpace(declaredJurisdiction)) return null;
        var applicable = policies.Where(x => x.JurisdictionCode == declaredJurisdiction &&
            x.EffectiveFrom <= intakeTime && (x.EffectiveUntil == null || x.EffectiveUntil > intakeTime)).ToArray();
        return applicable.Length == 1 ? applicable[0] : null;
    }
    public static bool IsActive(CaseReviewerGrant? grant, DateTimeOffset now) => grant != null &&
        grant.RevokedAt == null && grant.ValidFrom <= now && grant.ValidUntil > now;
    public IntakeCoordinatorScope RequireCoordinator(Guid actor)
    {
        var scope = directory.Coordinator(actor);
        if (scope == null || scope.ExpiresAt <= DateTimeOffset.UtcNow || string.IsNullOrWhiteSpace(scope.SourceReference))
            throw new UnauthorizedAccessException("An active intake coordinator office assignment is required.");
        return scope;
    }
    public async Task<CaseAssignmentState> ChangeAsync(Guid coordinator, Guid caseId, int expectedRevision,
        Guid? reviewerId, string officeCode, Guid documentId, string reason, DateTimeOffset validUntil, CancellationToken ct)
    {
        var scope = RequireCoordinator(coordinator);
        var state = await store.EnsureAsync(caseId, ct);
        if (state.Revision != expectedRevision) throw new InvalidOperationException("Assignment changed. Refresh before assigning.");
        if (state.OfficeCode == null ? !scope.CanManageUnassigned : state.OfficeCode != scope.OfficeCode)
            throw new UnauthorizedAccessException("This intake belongs to another office.");
        if (officeCode != scope.OfficeCode) throw new UnauthorizedAccessException("Only the coordinator's own office may be assigned.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) throw new ArgumentException("A reason (maximum 2000 characters) is required.");
        var now = DateTimeOffset.UtcNow;
        CaseReviewerGrant? next = null;
        if (reviewerId != null)
        {
            if (!directory.Reviewers.Any(x => x.ActorId == reviewerId && x.OfficeCode == officeCode))
                throw new UnauthorizedAccessException("Reviewer is not eligible for this office.");
            if (!await store.DocumentBelongsAsync(caseId, documentId, ct)) throw new ArgumentException("Select a document belonging to this case.");
            if (validUntil <= now || validUntil > scope.ExpiresAt || validUntil > now.AddYears(1))
                throw new ArgumentException("Validity must be in the future and within the coordinator's authority (maximum one year).");
            next = new(Guid.NewGuid(), reviewerId.Value, officeCode, documentId, ProposalReviewCapabilities.ToArray(),
                coordinator, reason.Trim(), now, now, validUntil);
        }
        var history = state.History.ToList();
        if (state.Active is { } prior) history.Add(prior with { RevokedAt = now, RevokedBy = coordinator, RevocationReason = reason.Trim() });
        return await store.SaveAsync(state with { Revision = state.Revision + 1, OfficeCode = officeCode,
            Active = next, History = history }, expectedRevision, ct);
    }
}

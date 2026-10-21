namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed record IntakeJurisdictionPolicy(Guid Id, string Identifier, string Version, string JurisdictionCode,
    string OfficeCode, string SourceReference, Guid PublishedBy, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil);
public sealed record CaseReviewerGrant(Guid Id, Guid ActorId, string OfficeCode, Guid DocumentId,
    string[] Capabilities, Guid AssignedBy, string Reason, DateTimeOffset AssignedAt, DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil, DateTimeOffset? RevokedAt = null, Guid? RevokedBy = null, string? RevocationReason = null);
public sealed record CaseAssignmentState(Guid CaseId, int Revision, string? JurisdictionCode, string? OfficeCode,
    Guid? RoutingPolicyId, string? RoutingPolicyVersion, string RoutingState, CaseReviewerGrant? Active,
    IReadOnlyList<CaseReviewerGrant> History);
public sealed record IntakeCaseSummary(Guid CaseId, string ApplicationReference, Guid ApplicantId, DateTime CreatedAt,
    string Status, Guid[] DocumentIds, CaseAssignmentState Assignment);
public sealed record EligibleCaseReviewer(Guid ActorId, string DisplayName, string OfficeCode);
public sealed record IntakeCoordinatorScope(Guid ActorId, string OfficeCode, bool CanManageUnassigned,
    DateTimeOffset ExpiresAt, string SourceReference);
public interface ICaseAssignmentStore
{
    Task<CaseAssignmentState?> ReadAsync(Guid caseId, CancellationToken ct = default);
    Task<CaseAssignmentState> EnsureAsync(Guid caseId, CancellationToken ct = default);
    Task<IReadOnlyList<IntakeCaseSummary>> ListAsync(Guid? applicant, Guid? reviewer, string? coordinatorOffice,
        bool includeUnassigned, CancellationToken ct = default);
    Task<CaseAssignmentState> SaveAsync(CaseAssignmentState state, int expectedRevision, CancellationToken ct = default);
    Task<bool> DocumentBelongsAsync(Guid caseId, Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<IntakeJurisdictionPolicy>> PoliciesAsync(CancellationToken ct = default);
    Task PublishPolicyAsync(IntakeJurisdictionPolicy policy, CancellationToken ct = default);
}
public interface IIntakeAssignmentDirectory
{
    IntakeCoordinatorScope? Coordinator(Guid actorId);
    IReadOnlyList<EligibleCaseReviewer> Reviewers { get; }
}

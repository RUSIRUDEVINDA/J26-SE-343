using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

// No update/delete operation is exposed. Runtime receives SELECT/INSERT only.
public sealed class ComplaintAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid ComplaintId { get; set; }
    public int Revision { get; set; }
    public Guid TargetActorId { get; set; }
    public string TargetOfficeCode { get; set; } = "";
    public Guid AssignedByActorId { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public Guid SubmissionKey { get; set; }
    public string InputSha256 { get; set; } = "";
    public Guid? PreviousAssignmentId { get; set; }
    public string DirectorySnapshotSha256 { get; set; } = "";
    public Guid AuthorizationGrantId { get; set; }
    public string AuthorizationSourceReference { get; set; } = "";
    public string EventCanonicalJson { get; set; } = "";
    public string EventSha256 { get; set; } = "";
    public ComplaintAssignment ToRecord() => new(Id, ComplaintId, Revision, TargetActorId, TargetOfficeCode,
        AssignedByActorId, AssignedAtUtc, Reason, SubmissionKey, InputSha256, PreviousAssignmentId,
        DirectorySnapshotSha256, AuthorizationGrantId, AuthorizationSourceReference, EventCanonicalJson, EventSha256);
}

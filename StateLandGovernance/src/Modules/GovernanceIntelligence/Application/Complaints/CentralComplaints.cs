using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StateLandGovernance.GovernanceIntelligence.Application.Complaints;

public sealed class CentralComplaintOptions
{
    public bool Enabled { get; set; }
    public bool WritesEnabled { get; set; }
    public CentralComplaintGrant[] Grants { get; set; } = [];
    public string? GrantFilePath { get; set; }
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CentralComplaintGrant
{
    // Parameterless binding prevents the configuration binder from binding an array both through
    // a positional constructor and again through its property. Duplicate grants still fail closed.
    public CentralComplaintGrant() { }
    public CentralComplaintGrant(Guid actorId, Guid grantId, string institution, string[] permissions,
        DateTimeOffset validFromUtc, DateTimeOffset? expiresAtUtc, string sourceReference)
    {
        ActorId=actorId; GrantId=grantId; Institution=institution; Permissions=permissions;
        ValidFromUtc=validFromUtc; ExpiresAtUtc=expiresAtUtc; SourceReference=sourceReference;
    }
    public Guid ActorId { get; set; }
    public Guid GrantId { get; set; }
    public string Institution { get; set; } = "";
    public string[] Permissions { get; set; } = [];
    public string[] ReviewOperations { get; set; } = [];
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool Persistent { get; set; }
    public bool Revoked { get; set; }
    public string SourceReference { get; set; } = "";
}
// The host supplies fresh server-owned permissions; never derived from browser claims.
public interface IComplaintCentralGrantSource
{
    Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct);
}
public sealed record ComplaintOfficer(Guid ActorId, string DisplayName, string OfficeCode);
public sealed record ComplaintDirectorySnapshot(ComplaintOfficer[] Officers, string Sha256);
// Implemented only in the composition host, using fresh read-only identity storage.
public interface IComplaintOfficerDirectory
{
    Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct);
    Task<bool> IsCommissionerAsync(Guid actor, CancellationToken ct);
}
public interface IComplaintActorContext { Guid ActorId { get; } }
public sealed record AssignComplaint(Guid SubmissionKey, int ExpectedAssignmentRevision, Guid TargetActorId, string Reason, int? ExpectedHandlingRevision = null);
public sealed record ComplaintAssignment(Guid Id, Guid ComplaintId, int Revision, Guid TargetActorId,
    string TargetOfficeCode, Guid AssignedByActorId, DateTimeOffset AssignedAtUtc, string Reason,
    Guid SubmissionKey, string InputSha256, Guid? PreviousAssignmentId, string DirectorySnapshotSha256,
    Guid AuthorizationGrantId, string AuthorizationSourceReference, string EventCanonicalJson, string EventSha256);
public sealed record ComplaintAssignmentResult(ComplaintAssignment Event, ComplaintAssignment Current, bool Reused, HandlingState? EffectiveHandling = null);
public sealed record CentralComplaintSummary(Guid Id, string Subject, DateTimeOffset SubmittedAtUtc, string ClassificationState, string ReviewStatus);
public sealed record CentralComplaintItem(CentralComplaintSummary Complaint, ComplaintAssignment? Assignment, bool TargetEligible, HandlingState? Handling = null);
public sealed record CentralComplaintPage(IReadOnlyList<CentralComplaintItem> Items, string? NextCursor);
public sealed record CentralComplaintDetail(RegisteredComplaintDetail Detail, ComplaintAssignment? Assignment, bool TargetEligible, ComplaintHandlingView? Handling = null);
public interface IComplaintRoutingStore
{
    Task<ComplaintAssignment?> CurrentAsync(Guid complaint, CancellationToken ct);
    Task<IReadOnlyList<ComplaintAssignment>> HistoryAsync(Guid complaint, CancellationToken ct);
    Task<IReadOnlyList<RegisteredComplaint>> AssignedAsync(Guid actor, CancellationToken ct);
    Task<(IReadOnlyList<RegisteredComplaint> Items, string? Next)> QueueAsync(bool assigned, string? cursor, CancellationToken ct);
    Task<ComplaintAssignmentResult> AssignAsync(Guid complaint, AssignComplaint input, CancellationToken ct);
    Task<RegisteredComplaint> ReviewAsync(Guid actor, Guid complaint, int revision, string status, string reason, CancellationToken ct, int? expectedHandlingRevision = null);
}
public sealed class CentralComplaintUnavailableException : Exception { }

public sealed class ComplaintRoutingPolicy(CentralComplaintOptions options,
    IComplaintOfficerDirectory directory, TimeProvider clock, IComplaintCentralGrantSource? source = null)
{
    public const string Read = "C4ComplaintCentralRead", Assign = "C4ComplaintCentralAssign", Review = "C4ComplaintCentralReview";
    public bool Enabled => options.Enabled;
    public bool WritesEnabled => options.Enabled && options.WritesEnabled;
    public async Task<CentralComplaintGrant> RequireCentralAsync(Guid actor, bool write, CancellationToken ct)
    {
        if (!Enabled || (write && !options.WritesEnabled)) throw new CentralComplaintUnavailableException();
        var current = source == null ? options.Grants : await source.ReadAsync(ct);
        var grants = current.Where(x => x.ActorId == actor).ToArray();
        var now = clock.GetUtcNow();
        if (actor == Guid.Empty || grants.Length != 1) throw new UnauthorizedAccessException();
        var grant = grants[0];
        if ((grant.Persistent && source == null) || grant.Revoked || grant.GrantId == Guid.Empty || grant.Institution != "COMMISSIONER" ||
            grant.ValidFromUtc == default || grant.ValidFromUtc.Offset != TimeSpan.Zero ||
            (!grant.Persistent && grant.ExpiresAtUtc == null) ||
            (grant.ExpiresAtUtc is {} end && (end.Offset != TimeSpan.Zero || now >= end || end <= grant.ValidFromUtc)) ||
            now < grant.ValidFromUtc ||
            string.IsNullOrWhiteSpace(grant.SourceReference) || grant.SourceReference.Length > 500 ||
            grant.Permissions is null || grant.Permissions.Distinct().Count() != grant.Permissions.Length ||
            grant.ReviewOperations == null || grant.ReviewOperations.Distinct().Count()!=grant.ReviewOperations.Length ||
            grant.ReviewOperations.Any(x=>!ComplaintHandlingRules.ReviewOperations.Contains(x)) ||
            (grant.ReviewOperations.Length>0 && !grant.Permissions.Contains(Review)) ||
            grant.Permissions.Any(x => x is not (Read or Assign or Review)) || !grant.Permissions.Contains(Read) ||
            (write && !grant.Permissions.Contains(Assign)) ||
            current.Count(x => x.GrantId == grant.GrantId) != 1 ||
            !await directory.IsCommissionerAsync(actor, ct)) throw new UnauthorizedAccessException();
        return grant;
    }
    public async Task<CentralComplaintGrant> RequireReviewAsync(Guid actor,string operation,CancellationToken ct)
    {
        var grant=await RequireCentralAsync(actor,false,ct);
        if(!WritesEnabled)throw new CentralComplaintUnavailableException();
        if(!grant.Permissions.Contains(Review)||!grant.ReviewOperations.Contains(operation))throw new UnauthorizedAccessException();
        return grant;
    }
    public async Task<ComplaintDirectorySnapshot> DirectoryAsync(Guid actor, CancellationToken ct)
    {
        await RequireCentralAsync(actor, true, ct);
        var result = await directory.ReadAsync(ct);
        await RequireCentralAsync(actor, true, ct);
        return result;
    }
    public async Task<bool> EligibleAsync(Guid actor, CancellationToken ct) => Enabled &&
        (await directory.ReadAsync(ct)).Officers.Count(x => x.ActorId == actor) == 1;
    public Task<ComplaintDirectorySnapshot> SnapshotAsync(CancellationToken ct) => directory.ReadAsync(ct);
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string InputHash(Guid complaint, AssignComplaint input) => Hash(JsonSerializer.Serialize(new
        { ComplaintId = complaint, input.SubmissionKey, input.ExpectedAssignmentRevision, input.TargetActorId, input.Reason, input.ExpectedHandlingRevision }));
    public static string Canonical(ComplaintAssignment e) => JsonSerializer.Serialize(new
    {
        e.Id, e.ComplaintId, e.Revision, e.TargetActorId, e.TargetOfficeCode, e.AssignedByActorId,
        e.AssignedAtUtc, e.Reason, e.SubmissionKey, e.InputSha256, e.PreviousAssignmentId,
        e.DirectorySnapshotSha256, e.AuthorizationGrantId, e.AuthorizationSourceReference
    });
}

public sealed class CentralComplaintService(IComplaintActorContext actor, ComplaintRoutingPolicy policy,
    IComplaintRoutingStore routing, IRegisteredComplaintStore complaints,
    StateLandGovernance.GovernanceIntelligence.Application.Interfaces.IComplaintClassificationAssessmentStore classifications, IComplaintHandlingStore? handling = null)
{
    public async Task<CentralComplaintPage> ListAsync(string? assignment, string? cursor, CancellationToken ct, string? filter = null)
    {
        await policy.RequireCentralAsync(actor.ActorId, false, ct);
        if(assignment!=null&&filter!=null)throw new ArgumentException("Choose either assignment or handling filter, not both.");
        assignment??="unassigned";
        if (assignment is not ("assigned" or "unassigned")) throw new ArgumentException("Choose assigned or unassigned.");
        var page = filter!=null&&handling!=null?await handling.QueueAsync(filter,cursor,ct):await routing.QueueAsync(assignment == "assigned", cursor, ct);
        var snapshot = await policy.SnapshotAsync(ct);
        var items = new List<CentralComplaintItem>();
        foreach (var row in page.Items)
        {
            var current = await routing.CurrentAsync(row.Id, ct);
            items.Add(new(new(row.Id, row.Subject, row.SubmittedAtUtc, row.ClassificationState, row.ReviewStatus), current, current != null && snapshot.Officers.Any(x => x.ActorId == current.TargetActorId && x.OfficeCode == current.TargetOfficeCode),handling==null?null:await handling.CurrentAsync(row.Id,ct)));
        }
        await policy.RequireCentralAsync(actor.ActorId, false, ct);
        return new(items, page.Next);
    }
    public async Task<CentralComplaintDetail> DetailAsync(Guid id, CancellationToken ct)
    {
        await policy.RequireCentralAsync(actor.ActorId, false, ct);
        var row = await complaints.GetAsync(id, ct) ?? throw new KeyNotFoundException();
        var results = new List<StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification.ComplaintClassificationAssessment>();
        foreach (var attempt in row.Attempts.Where(x => x.AssessmentId != null))
        {
            var result = await classifications.GetByIdAsync(attempt.AssessmentId!.Value, ct);
            if (result != null) results.Add(result);
        }
        var current = await routing.CurrentAsync(id, ct);
        var eligible = current != null && (await policy.SnapshotAsync(ct)).Officers.Any(x => x.ActorId == current.TargetActorId && x.OfficeCode == current.TargetOfficeCode);
        await policy.RequireCentralAsync(actor.ActorId, false, ct);
        return new(new(row, results), current, eligible, handling==null?null:await handling.ViewAsync(id,true,true,ct));
    }
    public async Task<IReadOnlyList<ComplaintAssignment>> HistoryAsync(Guid id, CancellationToken ct)
    {
        await policy.RequireCentralAsync(actor.ActorId, false, ct);
        if (await complaints.GetAsync(id, ct) == null) throw new KeyNotFoundException();
        var result = await routing.HistoryAsync(id, ct);
        await policy.RequireCentralAsync(actor.ActorId, false, ct);
        return result;
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace StateLandGovernance.GovernanceIntelligence.Application.Complaints;

public sealed record HandlingState(int Revision, string Status, string Mode, Guid? ResponsibleActorId, Guid? AssignmentEventId, string Source);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComplaintEvidenceReference(string Kind, string Reference, string Label);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComplaintHandlingAction(Guid SubmissionKey, [property:JsonRequired] int ExpectedHandlingRevision, string Operation, string Reason,
    Dictionary<string,string> Details, ComplaintEvidenceReference[] EvidenceReferences, string PublicSummary);
public sealed record HandlingHistoryItem(Guid Id, int Revision, string Operation, string Status, DateTimeOffset AtUtc,
    string PublicSummary, Guid? ActorId, string? Reason, Dictionary<string,string>? Details, ComplaintEvidenceReference[]? EvidenceReferences);
public sealed record ComplaintHandlingView(HandlingState State, string[] AllowedActions, IReadOnlyList<HandlingHistoryItem> History);
public sealed record HandlingActionResult(Guid EventId, bool Reused, HandlingState Current);
public interface IComplaintHandlingStore
{
    Task<(IReadOnlyList<RegisteredComplaint> Items,string? Next)> QueueAsync(string filter,string? cursor,CancellationToken ct);
    Task<HandlingState> CurrentAsync(Guid complaint, CancellationToken ct);
    Task<bool> IsAssignedAsync(Guid actor, Guid complaint, CancellationToken ct);
    Task<ComplaintHandlingView> ViewAsync(Guid complaint, bool central, bool officer, CancellationToken ct);
    Task<HandlingActionResult> ActAsync(Guid complaint, ComplaintHandlingAction input, bool central, CancellationToken ct);
    Task<HandlingState> PrepareAssignmentAsync(Guid complaint, AssignComplaint input, CancellationToken ct);
    Task RecordAssignmentAsync(HandlingState previous, ComplaintAssignment assignment, AssignComplaint input, CancellationToken ct);
    Task RecordReviewAsync(Guid complaint, HandlingState previous, ComplaintReview review, CancellationToken ct);
}

// Complaint-management transitions only: never evaluates allegation truth or calls an engine.
public static class ComplaintHandlingRules
{
    public static readonly string[] ReviewOperations = ["TakeDirect","AssignDs","StartInvestigation","RecordNote","RequestInformation","RecordFindings","Resolve","Close","Reopen"];
    public static readonly string[] DsOperations = ["StartInvestigation","RecordNote","RequestInformation","RecordFindings"];
    public static bool Terminal(string state) => state is "Resolved" or "Closed";
    public static string Next(string status, string operation) => operation switch
    {
        "TakeDirect" when !Terminal(status) => "UnderCommissionerReview",
        "AssignDs" or "ReassignDs" when !Terminal(status) => "AssignedToDs",
        "StartInvestigation" when status is "UnderCommissionerReview" or "AssignedToDs" or "InformationRequested" or "Reopened" => "UnderInvestigation",
        "RecordNote" when status is not "Unassigned" && !Terminal(status) => status,
        "RequestInformation" when status is "UnderCommissionerReview" or "AssignedToDs" or "UnderInvestigation" or "Reopened" => "InformationRequested",
        "RecordFindings" when status == "UnderInvestigation" => status,
        "Resolve" when status == "UnderInvestigation" => "Resolved",
        "Close" when status == "Resolved" => "Closed",
        "Reopen" when Terminal(status) => "Reopened",
        "DsReviewRecorded" when status is not "Unassigned" && !Terminal(status) => status,
        _ => throw new ComplaintConcurrencyException()
    };
    public static void Validate(ComplaintHandlingAction input)
    {
        if(input.SubmissionKey==Guid.Empty || input.ExpectedHandlingRevision<0 || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length>2000 ||
           input.Details==null || input.EvidenceReferences==null || input.PublicSummary==null || input.PublicSummary.Length>2000 ||
           !ReviewOperations.Contains(input.Operation) || input.Operation=="AssignDs") throw new ArgumentException("Invalid complaint action.");
        var fields=input.Operation switch
        {
            "Resolve" => new[]{"outcome","findings","actionsTaken","decisionBasis"},
            "RecordFindings" => ["findings","actionsTaken"],
            "Reopen" => ["newInformation","proceduralReason"],
            "Close" => Array.Empty<string>(),
            _ => ["notes"]
        };
        if(input.Details.Keys.Any(x=>!fields.Contains(x))) throw new ArgumentException("Unexpected action details.");
        foreach(var pair in input.Details)
        {
            var limit=pair.Key is "findings" or "actionsTaken" or "newInformation" or "proceduralReason"?3000:pair.Key=="decisionBasis"?2000:10000;
            if(string.IsNullOrWhiteSpace(pair.Value)||pair.Value.Length>limit)throw new ArgumentException("Supply bounded, meaningful action details.");
        }
        if(input.Operation=="Reopen")
        {if(!input.Details.ContainsKey("newInformation")&&!input.Details.ContainsKey("proceduralReason"))throw new ArgumentException("Give the basis for reopening.");}
        else if(fields.Any(x=>!input.Details.ContainsKey(x)))throw new ArgumentException("Required action details are missing.");
        if(input.Operation=="Resolve" && input.Details["outcome"] is not ("ActionTaken" or "NoFurtherAction" or "UnableToDetermine"))throw new ArgumentException("Invalid outcome.");
        if(input.Operation is "Resolve" or "Close" or "Reopen" or "RequestInformation" && string.IsNullOrWhiteSpace(input.PublicSummary))throw new ArgumentException("An applicant-safe summary is required.");
        if(input.EvidenceReferences.Length>20 || input.EvidenceReferences.Any(x=>x==null || x.Kind is not ("C4Record" or "ExternalReference") ||
           string.IsNullOrWhiteSpace(x.Reference)||x.Reference.Length>500||string.IsNullOrWhiteSpace(x.Label)||x.Label.Length>200))throw new ArgumentException("Invalid evidence references.");
        if(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(input.Details))>32768)throw new ArgumentException("Action details are too large.");
    }
    public static string CanonicalJson(string json)
    {
        using var document=JsonDocument.Parse(json);
        object? Normalize(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => e.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal).ToDictionary(x=>x.Name,x=>Normalize(x.Value)),
            JsonValueKind.Array => e.EnumerateArray().Select(Normalize).ToArray(),
            JsonValueKind.String => e.GetString(), JsonValueKind.Number => e.GetDecimal(),
            JsonValueKind.True => true, JsonValueKind.False => false, _ => null
        };
        return JsonSerializer.Serialize(Normalize(document.RootElement));
    }
}

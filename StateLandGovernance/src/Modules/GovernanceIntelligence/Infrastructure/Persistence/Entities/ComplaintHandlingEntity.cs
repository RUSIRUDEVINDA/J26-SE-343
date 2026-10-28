using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class ComplaintHandlingEntity
{
    public Guid Id { get; set; }
    public Guid ComplaintId { get; set; }
    public int Revision { get; set; }
    public Guid? PreviousEventId { get; set; }
    public string Operation { get; set; } = "";
    public string FromState { get; set; } = "";
    public string ToState { get; set; } = "";
    public string HandlingMode { get; set; } = "";
    public Guid? ResponsibleActorId { get; set; }
    public Guid? AssignmentEventId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset ActedAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public string DetailsJson { get; set; } = "";
    public string EvidenceReferencesJson { get; set; } = "";
    public string PublicSummary { get; set; } = "";
    public Guid SubmissionKey { get; set; }
    public string InputSha256 { get; set; } = "";
    public Guid? AuthorizationGrantId { get; set; }
    public string? AuthorizationSourceReference { get; set; }
    public string EventCanonicalJson { get; set; } = "";
    public string EventSha256 { get; set; } = "";
    public string Canonical() => ComplaintHandlingRules.CanonicalJson(JsonSerializer.Serialize(new {
        Schema="c4-complaint-handling/1",Id,ComplaintId,Revision,PreviousEventId,Operation,FromState,ToState,HandlingMode,
        ResponsibleActorId,AssignmentEventId,ActorId,ActedAtUtc,Reason,
        Details=JsonSerializer.Deserialize<JsonElement>(DetailsJson),EvidenceReferences=JsonSerializer.Deserialize<JsonElement>(EvidenceReferencesJson),
        PublicSummary,SubmissionKey,InputSha256,AuthorizationGrantId,AuthorizationSourceReference
    }));
}

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public sealed class RegulatoryFollowupEntity
{
    public Guid Id { get; set; }
    public Guid EvaluationId { get; set; }
    public string RuleCode { get; set; } = "";
    public string RuleVersion { get; set; } = "";
    public Guid ActorId { get; set; }
    public Guid SubmissionKey { get; set; }
    public string Operation { get; set; } = "";
    public string? ReviewOutcome { get; set; }
    public string? MissingInformation { get; set; }
    public string Remarks { get; set; } = "";
    public string ReferencesJson { get; set; } = "[]";
    public string Status { get; set; } = "Recorded";
    public int Revision { get; set; }
    public Guid? PreviousEventId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string InputSha256 { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public string EventSha256 { get; set; } = "";
    [NotMapped] public string OriginalFindingSha256 { get; set; } = "";
    public string Canonical(string findingHash) => JsonSerializer.Serialize(new
    {
        Id, EvaluationId, RuleCode, RuleVersion, ActorId, SubmissionKey, Operation, ReviewOutcome,
        MissingInformation, Remarks, References = JsonSerializer.Deserialize<string[]>(ReferencesJson), Status,
        Revision, PreviousEventId, CreatedAtUtc, InputSha256, OriginalFindingSha256 = findingHash
    }, RegulatoryJson.Options);
}

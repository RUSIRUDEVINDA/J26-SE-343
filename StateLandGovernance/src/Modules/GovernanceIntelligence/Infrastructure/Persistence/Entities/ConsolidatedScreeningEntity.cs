namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class ConsolidatedScreeningEntity
{
    public Guid Id { get; set; }
    public string CaseId { get; set; } = "";
    public string ProposalVersion { get; set; } = "";
    public string SnapshotSha256 { get; set; } = "";
    public DateTimeOffset EvaluatedAtUtc { get; set; }
    public string ResultJson { get; set; } = "";
}

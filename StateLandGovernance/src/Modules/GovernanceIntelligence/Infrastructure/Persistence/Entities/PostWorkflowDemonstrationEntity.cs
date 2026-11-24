namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public sealed class PostWorkflowDemonstrationEntity
{
    public Guid Id { get; set; }
    public Guid CreatorActorId { get; set; }
    public string Scenario { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid WorkflowRunId { get; set; }
    public Guid ProjectionId { get; set; }
    public Guid? AssessmentId { get; set; }
    public string State { get; set; } = "Pending";
    public string? Failure { get; set; }
    public int Attempt { get; set; } = 1;
    public string AttemptsJson { get; set; } = "[]";
}

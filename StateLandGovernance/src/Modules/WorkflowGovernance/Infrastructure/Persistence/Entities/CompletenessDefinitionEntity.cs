namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

/// <summary>Approved configuration. A changed definition requires a new version.</summary>
public sealed class CompletenessDefinitionEntity
{
    public string Kind { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid ApprovedByActorId { get; set; }
}

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;
public sealed class CaseAssignmentEntity
{
    public Guid CaseId { get; set; }
    public int Revision { get; set; }
    public string StateJson { get; set; } = "{}";
}
public sealed class IntakeJurisdictionPolicyEntity
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = "";
    public string Version { get; set; } = "";
    public string PolicyJson { get; set; } = "{}";
}

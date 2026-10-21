namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for versioned InstitutionalRoutingConfig.
/// Maps to workflow_governance.institutional_routing_configs.
/// </summary>
public sealed class InstitutionalRoutingConfigEntity
{
    public Guid Id { get; set; }
    public string RuleSetName { get; set; } = string.Empty;
    public string RuleSetVersion { get; set; } = string.Empty;
    public int StandardStageDurationDays { get; set; }
    public int DivisionalSecretariatDurationDays { get; set; }
    public int CommissionerDurationDays { get; set; }
    public string StandardCapability { get; set; } = string.Empty;
    public string DivisionalSecretaryCapability { get; set; } = string.Empty;
    public string CommissionerCapability { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

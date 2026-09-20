using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class RuleParameterEntity
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string ParameterValue { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public ComplianceRuleEntity? Rule { get; set; }
}

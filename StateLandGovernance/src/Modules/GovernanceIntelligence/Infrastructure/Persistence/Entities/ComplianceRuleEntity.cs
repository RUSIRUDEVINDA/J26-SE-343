using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class ComplianceRuleEntity
{
    public Guid Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleVersion { get; set; } = "1.0";
    public Guid? SourceId { get; set; }
    public string? SourceSection { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string? CalculationKey { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public RegulatorySourceEntity? Source { get; set; }
    public List<RuleParameterEntity> Parameters { get; set; } = new();
}

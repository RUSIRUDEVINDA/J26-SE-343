using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class RegulatorySourceEntity
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentVersion { get; set; } = string.Empty;
    public string? GazetteNumber { get; set; }
    public DateTime? PublishedDate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? DocumentReference { get; set; }
    public string? Checksum { get; set; }
    public bool IsActive { get; set; } = true;
}

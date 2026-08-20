using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Infrastructure persistence entity representing a governance audit record table row in PostgreSQL.
/// Keeps database concerns encapsulated away from the Domain GovernanceAuditRecord entity.
/// </summary>
public sealed class GovernanceAuditRecordEntity
{
    public Guid Id { get; set; }
    public int EngineType { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

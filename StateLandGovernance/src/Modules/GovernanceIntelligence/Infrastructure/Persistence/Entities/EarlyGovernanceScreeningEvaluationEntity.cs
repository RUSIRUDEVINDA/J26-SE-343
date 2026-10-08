using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Entity Framework Core entity representing an early governance screening evaluation snapshot table row.
/// </summary>
public class EarlyGovernanceScreeningEvaluationEntity
{
    public Guid AssessmentId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string InputVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int SnapshotSchemaVersion { get; set; }
    public string ResultSnapshotJson { get; set; } = string.Empty;
}

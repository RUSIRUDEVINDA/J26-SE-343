using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Entities;

/// <summary>
/// Domain entity representing a recorded governance audit trail/action outcome.
/// </summary>
public sealed class GovernanceAuditRecord
{
    public Guid Id { get; }
    public EngineType EngineType { get; }
    public string ActionName { get; }
    public string Status { get; }
    public string Details { get; }
    public DateTime Timestamp { get; }

    private GovernanceAuditRecord(Guid id, EngineType engineType, string actionName, string status, string details, DateTime timestamp)
    {
        Id = id;
        EngineType = engineType;
        ActionName = actionName;
        Status = status;
        Details = details;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Factory method to create a new GovernanceAuditRecord.
    /// </summary>
    public static GovernanceAuditRecord Create(EngineType engineType, string actionName, string status, string details)
    {
        return Create(engineType, actionName, status, details, DateTime.UtcNow);
    }

    /// <summary>
    /// Factory method to create a new GovernanceAuditRecord with an explicit timestamp.
    /// </summary>
    public static GovernanceAuditRecord Create(EngineType engineType, string actionName, string status, string details, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            throw new ArgumentException("Action name cannot be empty.", nameof(actionName));
        }

        return new GovernanceAuditRecord(
            Guid.NewGuid(),
            engineType,
            actionName,
            status ?? "Unknown",
            details ?? string.Empty,
            timestamp);
    }
}

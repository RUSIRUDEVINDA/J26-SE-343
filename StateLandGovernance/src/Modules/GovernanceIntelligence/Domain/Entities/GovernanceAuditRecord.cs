using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

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
    public string HashVersion { get; }

    private GovernanceAuditRecord(Guid id, EngineType engineType, string actionName, string status, string details, DateTime timestamp)
        : this(id, engineType, actionName, status, details, timestamp, GovernanceAuditHasher.CurrentFormatVersion)
    {
    }

    private GovernanceAuditRecord(Guid id, EngineType engineType, string actionName, string status, string details, DateTime timestamp, string hashVersion)
    {
        Id = id;
        EngineType = engineType;
        ActionName = actionName;
        Status = status;
        Details = details;
        Timestamp = timestamp;
        HashVersion = hashVersion;
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

    /// <summary>
    /// Rehydrates an immutable audit record using the persisted canonical hash protocol version.
    /// Rows created before version metadata existed are interpreted as the original AUDIT-V1 protocol.
    /// </summary>
    public static GovernanceAuditRecord Rehydrate(
        Guid id,
        EngineType engineType,
        string actionName,
        string status,
        string details,
        DateTime timestamp,
        string? hashVersion)
    {
        return new GovernanceAuditRecord(
            id,
            engineType,
            actionName,
            status,
            details,
            timestamp,
            string.IsNullOrWhiteSpace(hashVersion) ? GovernanceAuditHasher.AuditV1 : hashVersion);
    }
}

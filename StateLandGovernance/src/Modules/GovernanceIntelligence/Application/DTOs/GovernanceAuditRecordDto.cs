using System;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// Data Transfer Object representing a GovernanceAuditRecord for API response presentation.
/// </summary>
public sealed record GovernanceAuditRecordDto(
    Guid Id,
    string EngineType,
    string ActionName,
    string Status,
    string Details,
    DateTime Timestamp
);

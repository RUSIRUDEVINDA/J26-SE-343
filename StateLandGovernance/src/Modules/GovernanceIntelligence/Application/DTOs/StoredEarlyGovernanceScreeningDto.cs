using System;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// Immutable wrapper record for an early governance screening evaluation snapshot stored in the audit repository.
/// Preserves evaluation metadata, schema versioning, and caller-supplied snapshot provenance.
/// </summary>
public sealed record StoredEarlyGovernanceScreeningDto(
    Guid AssessmentId,
    DateTimeOffset CreatedAtUtc,
    int SnapshotSchemaVersion,
    EarlyGovernanceScreeningResultDto Result
);

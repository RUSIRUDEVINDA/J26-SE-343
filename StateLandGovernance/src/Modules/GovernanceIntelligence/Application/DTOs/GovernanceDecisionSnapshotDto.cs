using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// DTO representing an external governance decision snapshot input.
/// </summary>
public sealed record GovernanceDecisionSnapshotDto(
    string DecisionId,
    string SubjectId,
    string InstitutionName,
    string AuthorityLevel,
    string DecisionType,
    string ProposedUse,
    DateTime EffectiveFrom,
    DateTime EffectiveTo,
    string RegulatoryReference,
    IReadOnlyList<string>? IncompatibleRegulatoryReferences = null,
    string? MandateKey = null,
    string? MandateMode = null,
    string? LandUseCode = null,
    IReadOnlyList<string>? IncompatibleLandUseCodes = null
);

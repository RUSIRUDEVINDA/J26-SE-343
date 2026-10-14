using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Source-backed legal and operational provenance metadata for a regulatory rule.
/// </summary>
public sealed record RuleSourceMetadata(
    RuleSourceType SourceType,
    string SourceAuthority,
    string SourceDocument,
    string SourceSection,
    string? SourcePage = null,
    string? SourceVersion = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null
);

using System;
using System.Collections.Generic;
using System.Linq;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing a snapshot of a governance decision issued by an institution.
/// </summary>
public sealed record GovernanceDecisionSnapshot
{
    public string DecisionId { get; init; }
    public string SubjectId { get; init; }
    public string InstitutionName { get; init; }
    public string AuthorityLevel { get; init; }
    public string DecisionType { get; init; }
    public string ProposedUse { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime EffectiveTo { get; init; }
    public string RegulatoryReference { get; init; }
    
    // Explicit evidence collections for deterministic conflict checking
    public IReadOnlyList<string> IncompatibleRegulatoryReferences { get; init; }
    public string MandateKey { get; init; }
    public string MandateMode { get; init; }
    public string LandUseCode { get; init; }
    public IReadOnlyList<string> IncompatibleLandUseCodes { get; init; }

    public GovernanceDecisionSnapshot(
        string decisionId,
        string subjectId,
        string institutionName,
        string authorityLevel,
        string decisionType,
        string proposedUse,
        DateTime effectiveFrom,
        DateTime effectiveTo,
        string regulatoryReference,
        IReadOnlyList<string>? incompatibleRegulatoryReferences = null,
        string? mandateKey = null,
        string? mandateMode = null,
        string? landUseCode = null,
        IReadOnlyList<string>? incompatibleLandUseCodes = null)
    {
        DecisionId = decisionId;
        SubjectId = subjectId;
        InstitutionName = institutionName;
        AuthorityLevel = authorityLevel;
        DecisionType = decisionType;
        ProposedUse = proposedUse;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        RegulatoryReference = regulatoryReference;

        // Defensively copy collections to enforce strict read-only and immutable invariants
        IncompatibleRegulatoryReferences = incompatibleRegulatoryReferences != null
            ? incompatibleRegulatoryReferences.ToList().AsReadOnly()
            : Array.Empty<string>();

        MandateKey = mandateKey ?? string.Empty;
        MandateMode = mandateMode ?? string.Empty;
        LandUseCode = landUseCode ?? string.Empty;

        IncompatibleLandUseCodes = incompatibleLandUseCodes != null
            ? incompatibleLandUseCodes.ToList().AsReadOnly()
            : Array.Empty<string>();
    }
}

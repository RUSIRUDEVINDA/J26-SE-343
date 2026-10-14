namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for a Component 1 land/case governance projection stored in the
/// <c>governance_intelligence.component1_case_projections</c> table.
/// </summary>
public sealed class Component1CaseProjectionEntity
{
    public Guid ProjectionId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string LandParcelId { get; set; } = string.Empty;
    public string DivisionCode { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string ProvinceName { get; set; } = string.Empty;
    public string GnDivisionCode { get; set; } = string.Empty;
    public string RequestedLandUseCategory { get; set; } = string.Empty;
    public decimal ExtentHa { get; set; }
    public string OwnershipEvidenceStatus { get; set; } = string.Empty;
    public string ProtectedZoneOverlapState { get; set; } = string.Empty;
    public string OverlappingSensitiveZoneCodesJson { get; set; } = "[]";
    public string BoundaryConfirmationStatus { get; set; } = string.Empty;
    public bool EncumbranceOrDisputeFlag { get; set; }
    public string ParcelEvidenceRefsJson { get; set; } = "[]";
    public string InputSha256 { get; set; } = string.Empty;

    // Shared Provenance columns
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceRecordId { get; set; } = string.Empty;
    public string SourceRecordVersion { get; set; } = string.Empty;
    public DateTimeOffset SourceObservedAtUtc { get; set; }
    public bool IsSynthetic { get; set; }
    public string ProvenanceKind { get; set; } = string.Empty;
    public string? SyntheticSeedId { get; set; }
    public string? SyntheticSeedVersion { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string Purpose { get; set; } = string.Empty;

    public DateTimeOffset IngestedAtUtc { get; set; }
}

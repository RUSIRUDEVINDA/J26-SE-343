namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum AdministrativeLocationVerificationStatus
{
    Verified = 1,
    Mismatch = 2,
    Unavailable = 3
}

public enum AdministrativeLocationGeometryBasis
{
    Boundary = 1,
    Centroid = 2
}

public sealed class AdministrativeLocationVerificationResult
{
    public required Guid ParcelId { get; init; }

    public required string StoredProvince { get; init; }

    public required string? DetectedProvince { get; init; }

    public required bool? ProvinceMatches { get; init; }

    public required string StoredDistrict { get; init; }

    public required string? DetectedDistrict { get; init; }

    public required bool? DistrictMatches { get; init; }

    public required AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }

    public required AdministrativeLocationVerificationStatus Status { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required AttributeProvenanceDto StoredProvinceProvenance { get; init; }

    public required AttributeProvenanceDto StoredDistrictProvenance { get; init; }

    public AttributeProvenanceDto? DetectedProvinceProvenance { get; init; }

    public AttributeProvenanceDto? DetectedDistrictProvenance { get; init; }

    public required AttributeProvenanceDto BoundarySourceProvenance { get; init; }
}

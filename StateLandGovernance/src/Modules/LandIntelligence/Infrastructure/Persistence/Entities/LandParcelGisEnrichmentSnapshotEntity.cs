using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class LandParcelGisEnrichmentSnapshotEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public LandParcelGisEnrichmentOverallStatus OverallStatus { get; set; }

    public AdministrativeLocationVerificationStatus? AdministrativeStatus { get; set; }

    public string? DetectedProvince { get; set; }

    public string? DetectedDistrict { get; set; }

    public bool? ProvinceMatches { get; set; }

    public bool? DistrictMatches { get; set; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; set; }

    public string SourceName { get; set; } = null!;

    public DateTimeOffset EnrichedAt { get; set; }

    public LandParcelEntity LandParcel { get; set; } = null!;
}

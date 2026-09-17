using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class ParcelDerivedSoilGroupEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public Guid SoilGroupReferenceId { get; set; }

    public string SoilGroupName { get; set; } = null!;

    public decimal? OverlapAreaSquareMeters { get; set; }

    public decimal? OverlapPercentage { get; set; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; set; }

    public string SourceName { get; set; } = null!;

    public string SourceLayer { get; set; } = null!;

    public string ProvenanceJson { get; set; } = null!;

    public DateTimeOffset DerivedAt { get; set; }

    public LandParcelEntity LandParcel { get; set; } = null!;
}

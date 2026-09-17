using NetTopologySuite.Geometries;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

public sealed class GisSoilErosionObservationEntity : GisReferenceEntityBase
{
    public string? ObservationClass { get; set; }

    public string? Description { get; set; }

    public decimal? ErosionRate { get; set; }

    public Point Location { get; set; } = null!;
}

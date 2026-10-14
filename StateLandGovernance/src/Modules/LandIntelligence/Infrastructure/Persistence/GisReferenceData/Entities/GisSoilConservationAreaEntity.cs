using NetTopologySuite.Geometries;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

public sealed class GisSoilConservationAreaEntity : GisReferenceEntityBase
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public MultiPolygon Boundary { get; set; } = null!;
}

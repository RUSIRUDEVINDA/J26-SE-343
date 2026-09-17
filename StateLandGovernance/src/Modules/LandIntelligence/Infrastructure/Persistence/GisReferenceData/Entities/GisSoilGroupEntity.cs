using NetTopologySuite.Geometries;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

public sealed class GisSoilGroupEntity : GisReferenceEntityBase
{
    public string Name { get; set; } = null!;

    public MultiPolygon Boundary { get; set; } = null!;
}

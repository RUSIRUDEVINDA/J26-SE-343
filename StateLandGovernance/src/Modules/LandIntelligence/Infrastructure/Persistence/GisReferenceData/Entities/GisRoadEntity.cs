using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

public sealed class GisRoadEntity : GisReferenceEntityBase
{
    public string? Name { get; set; }

    public GisRoadType RoadType { get; set; }

    public MultiLineString Geometry { get; set; } = null!;
}

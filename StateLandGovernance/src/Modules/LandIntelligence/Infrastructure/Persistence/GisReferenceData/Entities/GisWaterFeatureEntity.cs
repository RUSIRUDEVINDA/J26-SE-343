using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

public sealed class GisWaterFeatureEntity : GisReferenceEntityBase
{
    public string? Name { get; set; }

    public GisWaterFeatureType FeatureType { get; set; }

    public Geometry Geometry { get; set; } = null!;
}

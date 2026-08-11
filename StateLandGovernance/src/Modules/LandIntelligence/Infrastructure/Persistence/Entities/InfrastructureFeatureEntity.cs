using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class InfrastructureFeatureEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public LandParcelEntity LandParcel { get; set; } = null!;

    public InfrastructureFeatureType Type { get; set; }

    public string Name { get; set; } = null!;

    public decimal? DistanceMeters { get; set; }

    public string? Description { get; set; }

    /// <summary>Feature location when known.</summary>
    public Point? Location { get; set; }

    public int SpatialReferenceSystemId { get; set; } = 4326;
}

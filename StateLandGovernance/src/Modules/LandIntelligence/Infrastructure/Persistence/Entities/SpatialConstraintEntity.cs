using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class SpatialConstraintEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public LandParcelEntity LandParcel { get; set; } = null!;

    public SpatialConstraintType Type { get; set; }

    public string Description { get; set; } = null!;

    public RestrictionSeverity Severity { get; set; }

    /// <summary>Constraint geometry (e.g. buffer zone, zoning polygon).</summary>
    public MultiPolygon? ConstraintGeometry { get; set; }

    public int SpatialReferenceSystemId { get; set; } = 4326;
}

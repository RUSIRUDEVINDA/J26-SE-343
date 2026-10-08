using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

public sealed class GisAdministrativeBoundaryEntity : GisReferenceEntityBase
{
    public string Name { get; set; } = null!;

    public GisAdministrativeBoundaryType BoundaryType { get; set; }

    public MultiPolygon Boundary { get; set; } = null!;
}

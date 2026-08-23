using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

internal static class ParcelEnrichmentGeometrySelector
{
    public static (Geometry Geometry, AdministrativeLocationGeometryBasis Basis)? Select(
        MultiPolygon? boundary,
        Point centroid)
    {
        if (boundary is not null && !boundary.IsEmpty)
        {
            return (boundary, AdministrativeLocationGeometryBasis.Boundary);
        }

        if (centroid is not null && !centroid.IsEmpty)
        {
            return (centroid, AdministrativeLocationGeometryBasis.Centroid);
        }

        return null;
    }
}

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

/// <summary>
/// Domain representation of parcel geometry without GIS infrastructure dependencies.
/// Boundary may hold WKT or an external geometry reference resolved by infrastructure later.
/// </summary>
public sealed record SpatialReference
{
    public double CentroidLatitude { get; }
    public double CentroidLongitude { get; }
    public string CoordinateSystem { get; }
    public string? BoundaryReference { get; }

    public SpatialReference(
        double centroidLatitude,
        double centroidLongitude,
        string coordinateSystem = "EPSG:4326",
        string? boundaryReference = null)
    {
        if (centroidLatitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(centroidLatitude));
        }

        if (centroidLongitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(centroidLongitude));
        }

        if (string.IsNullOrWhiteSpace(coordinateSystem))
        {
            throw new ArgumentException("Coordinate system is required.", nameof(coordinateSystem));
        }

        CentroidLatitude = centroidLatitude;
        CentroidLongitude = centroidLongitude;
        CoordinateSystem = coordinateSystem.Trim();
        BoundaryReference = string.IsNullOrWhiteSpace(boundaryReference)
            ? null
            : boundaryReference.Trim();
    }
}

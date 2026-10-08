namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

/// <summary>
/// WGS 84 geographic coordinate without GIS library dependencies.
/// </summary>
public sealed record GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoCoordinate(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        Latitude = latitude;
        Longitude = longitude;
    }
}

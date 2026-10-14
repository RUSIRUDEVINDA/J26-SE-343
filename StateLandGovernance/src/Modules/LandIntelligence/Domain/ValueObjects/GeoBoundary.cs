namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

/// <summary>
/// Simple polygon boundary represented by an exterior ring of coordinates.
/// </summary>
public sealed record GeoBoundary
{
    public IReadOnlyList<GeoCoordinate> ExteriorRing { get; }

    public GeoBoundary(IReadOnlyList<GeoCoordinate> exteriorRing)
    {
        if (exteriorRing is null || exteriorRing.Count < 3)
        {
            throw new ArgumentException("A boundary requires at least three coordinates.", nameof(exteriorRing));
        }

        ExteriorRing = exteriorRing;
    }
}

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// GeoJSON Polygon or MultiPolygon geometry for API requests (RFC 7946 positions as [longitude, latitude]).
/// </summary>
public sealed record GeoJsonPolygonDto
{
    public string Type { get; init; } = "Polygon";

    /// <summary>
    /// Polygon: [ exteriorRing[ [lng, lat], ... ] ].
    /// MultiPolygon: [ polygon[ ring[ [lng, lat], ... ] ], ... ].
    /// </summary>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>>? Coordinates { get; init; }
}

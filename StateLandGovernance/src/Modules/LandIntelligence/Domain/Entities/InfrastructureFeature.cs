using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

/// <summary>
/// Infrastructure feature associated with a land parcel, including proximity context.
/// </summary>
public class InfrastructureFeature : Entity
{
    public InfrastructureFeatureType Type { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal? DistanceMeters { get; private set; }
    public string? Description { get; private set; }
    public GeoCoordinate? Location { get; private set; }
    public AttributeProvenance? DistanceProvenance { get; private set; }

    private InfrastructureFeature()
    {
    }

    public InfrastructureFeature(
        InfrastructureFeatureType type,
        string name,
        decimal? distanceMeters = null,
        string? description = null,
        AttributeProvenance? distanceProvenance = null,
        GeoCoordinate? location = null,
        Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Infrastructure name is required.", nameof(name));
        }

        if (distanceMeters is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        }

        Type = type;
        Name = name.Trim();
        DistanceMeters = distanceMeters;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DistanceProvenance = distanceProvenance;
        Location = location;
    }
}

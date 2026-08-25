using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

public sealed class LandParcel : Entity
{
    private readonly List<SpatialConstraint> _spatialConstraints = [];
    private readonly List<EnvironmentalRestriction> _environmentalRestrictions = [];
    private readonly List<InfrastructureFeature> _infrastructureFeatures = [];
    private readonly List<RegulatoryReference> _regulatoryReferences = [];

    public ParcelIdentifier Identifier { get; private set; } = null!;
    public LandCategory Category { get; private set; } = null!;
    public LandUse? CurrentUse { get; private set; }
    public LandArea Area { get; private set; } = null!;
    public AdministrativeLocation Location { get; private set; } = null!;
    public SpatialReference Spatial { get; private set; } = null!;
    public LandCharacteristics? Characteristics { get; private set; }

    public IReadOnlyCollection<SpatialConstraint> SpatialConstraints => _spatialConstraints.AsReadOnly();
    public IReadOnlyCollection<EnvironmentalRestriction> EnvironmentalRestrictions =>
        _environmentalRestrictions.AsReadOnly();
    public IReadOnlyCollection<InfrastructureFeature> InfrastructureFeatures =>
        _infrastructureFeatures.AsReadOnly();
    public IReadOnlyCollection<RegulatoryReference> RegulatoryReferences =>
        _regulatoryReferences.AsReadOnly();

    private LandParcel()
    {
    }

    public LandParcel(
        ParcelIdentifier identifier,
        LandCategory category,
        LandArea area,
        AdministrativeLocation location,
        SpatialReference spatial,
        LandUse? currentUse = null,
        LandCharacteristics? characteristics = null)
    {
        Identifier = identifier;
        Category = category;
        Area = area;
        Location = location;
        Spatial = spatial;
        CurrentUse = currentUse;
        Characteristics = characteristics;
    }

    public void UpdateCurrentUse(LandUse landUse) => CurrentUse = landUse;

    public void UpdateCharacteristics(LandCharacteristics characteristics) =>
        Characteristics = characteristics;

    public void UpdateSpatial(SpatialReference spatial) => Spatial = spatial;

    public void ReplaceSpatialConstraints(IEnumerable<SpatialConstraint> constraints)
    {
        _spatialConstraints.Clear();
        _spatialConstraints.AddRange(constraints);
    }

    public void ReplaceEnvironmentalRestrictions(IEnumerable<EnvironmentalRestriction> restrictions)
    {
        _environmentalRestrictions.Clear();
        _environmentalRestrictions.AddRange(restrictions);
    }

    public void ReplaceInfrastructureFeatures(IEnumerable<InfrastructureFeature> features)
    {
        _infrastructureFeatures.Clear();
        _infrastructureFeatures.AddRange(features);
    }

    public void ReplaceRegulatoryReferences(IEnumerable<RegulatoryReference> references)
    {
        _regulatoryReferences.Clear();
        _regulatoryReferences.AddRange(references);
    }

    public void AddSpatialConstraint(SpatialConstraint constraint) =>
        _spatialConstraints.Add(constraint);

    public void AddEnvironmentalRestriction(EnvironmentalRestriction restriction) =>
        _environmentalRestrictions.Add(restriction);

    public void AddInfrastructureFeature(InfrastructureFeature feature) =>
        _infrastructureFeatures.Add(feature);

    public void AddRegulatoryReference(RegulatoryReference reference) =>
        _regulatoryReferences.Add(reference);
}

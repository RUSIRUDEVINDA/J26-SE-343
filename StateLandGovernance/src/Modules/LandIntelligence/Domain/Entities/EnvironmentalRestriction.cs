using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

public class EnvironmentalRestriction : Entity
{
    public EnvironmentalRestrictionType Type { get; private set; }
    public string Description { get; private set; } = null!;
    public RestrictionSeverity Severity { get; private set; }
    public AttributeProvenance? DataProvenance { get; private set; }

    private EnvironmentalRestriction()
    {
    }

    public EnvironmentalRestriction(
        EnvironmentalRestrictionType type,
        string description,
        RestrictionSeverity severity,
        AttributeProvenance? dataProvenance = null,
        Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Restriction description is required.", nameof(description));
        }

        Type = type;
        Description = description.Trim();
        Severity = severity;
        DataProvenance = dataProvenance;
    }
}

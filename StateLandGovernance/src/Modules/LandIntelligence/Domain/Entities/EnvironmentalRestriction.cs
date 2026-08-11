using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

public class EnvironmentalRestriction : Entity
{
    public EnvironmentalRestrictionType Type { get; private set; }
    public string Description { get; private set; } = null!;
    public RestrictionSeverity Severity { get; private set; }

    private EnvironmentalRestriction()
    {
    }

    public EnvironmentalRestriction(
        EnvironmentalRestrictionType type,
        string description,
        RestrictionSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Restriction description is required.", nameof(description));
        }

        Type = type;
        Description = description.Trim();
        Severity = severity;
    }
}

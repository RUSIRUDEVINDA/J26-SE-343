using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

public class SpatialConstraint : Entity
{
    public SpatialConstraintType Type { get; private set; }
    public string Description { get; private set; } = null!;
    public RestrictionSeverity Severity { get; private set; }

    private SpatialConstraint()
    {
    }

    public SpatialConstraint(
        SpatialConstraintType type,
        string description,
        RestrictionSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Constraint description is required.", nameof(description));
        }

        Type = type;
        Description = description.Trim();
        Severity = severity;
    }
}

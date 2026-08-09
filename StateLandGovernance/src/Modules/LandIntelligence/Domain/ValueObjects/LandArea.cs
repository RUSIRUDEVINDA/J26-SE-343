using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record LandArea
{
    public decimal Value { get; }
    public AreaUnit Unit { get; }

    public LandArea(decimal value, AreaUnit unit)
    {
        if (value <= 0)
        {
            throw new InvalidLandAreaException("Land area must be greater than zero.");
        }

        Value = value;
        Unit = unit;
    }
}

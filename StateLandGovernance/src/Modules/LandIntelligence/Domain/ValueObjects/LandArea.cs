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

        if (!Enum.IsDefined(unit))
        {
            throw new InvalidLandAreaException($"Unsupported area unit: {unit}.");
        }

        Value = value;
        Unit = unit;
    }

    public decimal ToHectares() => Unit switch
    {
        AreaUnit.Hectares => Value,
        AreaUnit.Acres => Value * 0.404686m,
        AreaUnit.SquareMeters => Value / 10_000m,
        _ => throw new InvalidLandAreaException($"Unsupported area unit: {Unit}.")
    };
}

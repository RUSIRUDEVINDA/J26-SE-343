namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct LandExtent : IEquatable<LandExtent>
{
    public decimal Value { get; }
    public MeasurementUnit Unit { get; }

    public LandExtent(decimal value, MeasurementUnit unit)
    {
        if (value <= 0m)
        {
            throw new InvalidLandExtentException("Requested extent cannot be negative or zero.");
        }

        if (string.IsNullOrWhiteSpace(unit.Name))
        {
            throw new InvalidLandExtentException("Measurement unit is required for land extent.");
        }

        Value = value;
        Unit = unit;
    }

    public override string ToString() => $"{Value} {Unit}";
}

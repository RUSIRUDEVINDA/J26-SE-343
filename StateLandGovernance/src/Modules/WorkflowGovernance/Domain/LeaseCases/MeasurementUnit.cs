namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct MeasurementUnit : IEquatable<MeasurementUnit>
{
    private static readonly Dictionary<string, string> RecognizedCanonicalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acre"] = "Acre",
        ["Acres"] = "Acre",
        ["Hectare"] = "Hectare",
        ["Hectares"] = "Hectare",
        ["Ha"] = "Hectare",
        ["Perch"] = "Perch",
        ["Perches"] = "Perch",
        ["SquareMeter"] = "SquareMeter",
        ["SquareMeters"] = "SquareMeter",
        ["Sqm"] = "SquareMeter",
        ["m2"] = "SquareMeter",
        ["SquareFoot"] = "SquareFoot",
        ["SquareFeet"] = "SquareFoot",
        ["Sqft"] = "SquareFoot",
        ["ft2"] = "SquareFoot",
        ["Rood"] = "Rood",
        ["Roods"] = "Rood"
    };

    public static readonly MeasurementUnit Acre = new("Acre", bypassValidation: true);
    public static readonly MeasurementUnit Hectare = new("Hectare", bypassValidation: true);
    public static readonly MeasurementUnit Perch = new("Perch", bypassValidation: true);
    public static readonly MeasurementUnit SquareMeter = new("SquareMeter", bypassValidation: true);
    public static readonly MeasurementUnit SquareFoot = new("SquareFoot", bypassValidation: true);
    public static readonly MeasurementUnit Rood = new("Rood", bypassValidation: true);

    public string Name { get; }

    public MeasurementUnit(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidMeasurementUnitException("Measurement unit cannot be null, empty, or whitespace.");
        }

        var trimmed = name.Trim();
        if (!RecognizedCanonicalNames.TryGetValue(trimmed, out var canonicalName))
        {
            throw new InvalidMeasurementUnitException($"Unit '{trimmed}' is not a recognised or approved unit of measurement.");
        }

        Name = canonicalName;
    }

    private MeasurementUnit(string canonicalName, bool bypassValidation)
    {
        Name = canonicalName;
    }

    public static MeasurementUnit From(string name) => new(name);

    public static bool IsRecognized(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return RecognizedCanonicalNames.ContainsKey(name.Trim());
    }

    public bool IsCompatibleWith(MeasurementUnit other)
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(other.Name))
            return false;

        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Name ?? string.Empty;
}

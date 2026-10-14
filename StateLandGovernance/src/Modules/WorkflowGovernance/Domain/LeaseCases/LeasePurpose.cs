namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct LeasePurpose : IEquatable<LeasePurpose>
{
    public static readonly LeasePurpose Commercial = new("Commercial");
    public static readonly LeasePurpose Agricultural = new("Agricultural");
    public static readonly LeasePurpose Industrial = new("Industrial");
    public static readonly LeasePurpose Tourism = new("Tourism");
    public static readonly LeasePurpose Residential = new("Residential");

    public string Value { get; }

    public LeasePurpose(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidProposalIntakeException("Lease purpose cannot be null, empty, or whitespace.");
        }

        Value = value.Trim();
    }

    public bool Equals(LeasePurpose other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    public override string ToString() => Value ?? string.Empty;
}

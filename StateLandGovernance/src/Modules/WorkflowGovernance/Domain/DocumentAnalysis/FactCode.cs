namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class FactCode : IEquatable<FactCode>
{
    public string Value { get; }

    public FactCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidExtractedFactException("FactCode is required.");
        var trimmed = value.Trim();
        if (trimmed.Length > 100) throw new InvalidExtractedFactException("FactCode is too long.");
        foreach (char c in trimmed)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-') throw new InvalidExtractedFactException("FactCode has invalid chars.");
        }
        Value = trimmed;
    }

    public bool Equals(FactCode? other)
    {
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }
    public override bool Equals(object? obj) => Equals(obj as FactCode);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
}

namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly struct ExtractedFactId : IEquatable<ExtractedFactId>
{
    public Guid Value { get; }

    public ExtractedFactId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidExtractedFactException("Id cannot be empty.");
        Value = value;
    }

    public bool Equals(ExtractedFactId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is ExtractedFactId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(ExtractedFactId left, ExtractedFactId right) => left.Equals(right);
    public static bool operator !=(ExtractedFactId left, ExtractedFactId right) => !left.Equals(right);
}


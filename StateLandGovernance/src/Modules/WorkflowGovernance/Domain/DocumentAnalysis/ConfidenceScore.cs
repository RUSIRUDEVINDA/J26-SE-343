namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ConfidenceScore : IEquatable<ConfidenceScore>
{
    public decimal Value { get; }
    public ConfidenceScore(decimal value)
    {
        if (value < 0.0m || value > 1.0m) throw new InvalidExtractedFactException("Confidence must be between 0.0 and 1.0.");
        Value = value;
    }
    public bool Equals(ConfidenceScore? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as ConfidenceScore);
    public override int GetHashCode() => Value.GetHashCode();
}

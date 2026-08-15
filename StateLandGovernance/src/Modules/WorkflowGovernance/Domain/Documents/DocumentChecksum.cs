namespace StateLandGovernance.WorkflowGovernance.Domain.Documents;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record DocumentChecksum
{
    public string Algorithm { get; }
    public string Value { get; }

    public DocumentChecksum(string algorithm, string value)
    {
        if (string.IsNullOrWhiteSpace(algorithm)) throw new InvalidChecksumException("Algorithm cannot be null or blank.");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidChecksumException("Value cannot be null or blank.");

        Algorithm = algorithm;
        Value = value;
    }

    public bool Equals(DocumentChecksum? other)
    {
        if (other is null) return false;
        return string.Equals(Algorithm, other.Algorithm, StringComparison.Ordinal) &&
               string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override int GetHashCode() => HashCode.Combine(Algorithm, Value);
}

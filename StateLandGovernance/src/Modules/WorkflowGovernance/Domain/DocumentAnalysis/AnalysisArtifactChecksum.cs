namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Globalization;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisArtifactChecksum : IEquatable<AnalysisArtifactChecksum>
{
    public string Algorithm { get; }
    public string Value { get; }

    public AnalysisArtifactChecksum(string algorithm, string value)
    {
        if (string.IsNullOrWhiteSpace(algorithm)) throw new InvalidAnalysisResultArtifactException("Algorithm is required.");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidAnalysisResultArtifactException("Value is required.");

        var trimmedAlg = algorithm.Trim();
        var trimmedVal = value.Trim();

        if (trimmedAlg.Length > 100) throw new InvalidAnalysisResultArtifactException("Algorithm is too long.");
        if (trimmedVal.Length > 500) throw new InvalidAnalysisResultArtifactException("Value is too long.");

        foreach(var c in trimmedAlg) { if (char.IsControl(c)) throw new InvalidAnalysisResultArtifactException("Algorithm has control characters."); }
        foreach(var c in trimmedVal) { if (char.IsControl(c)) throw new InvalidAnalysisResultArtifactException("Value has control characters."); }

        Algorithm = trimmedAlg;
        Value = trimmedVal;
    }

    public bool Equals(AnalysisArtifactChecksum? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Algorithm, other.Algorithm, StringComparison.Ordinal) &&
               string.Equals(Value, other.Value, StringComparison.Ordinal);
    }
    public override bool Equals(object? obj) => Equals(obj as AnalysisArtifactChecksum);
    public override int GetHashCode() => HashCode.Combine(Algorithm, Value);
}

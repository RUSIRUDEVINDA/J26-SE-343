namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly struct AnalysisResultArtifactId : IEquatable<AnalysisResultArtifactId>
{
    public Guid Value { get; }

    public AnalysisResultArtifactId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidAnalysisResultArtifactException("Id cannot be empty.");
        Value = value;
    }

    public bool Equals(AnalysisResultArtifactId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is AnalysisResultArtifactId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(AnalysisResultArtifactId left, AnalysisResultArtifactId right) => left.Equals(right);
    public static bool operator !=(AnalysisResultArtifactId left, AnalysisResultArtifactId right) => !left.Equals(right);
}


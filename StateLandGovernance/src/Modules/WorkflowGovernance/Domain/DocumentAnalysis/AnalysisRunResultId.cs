namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly struct AnalysisRunResultId : IEquatable<AnalysisRunResultId>
{
    public Guid Value { get; }

    public AnalysisRunResultId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidAnalysisRunResultException("Id cannot be empty.");
        Value = value;
    }

    public bool Equals(AnalysisRunResultId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is AnalysisRunResultId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(AnalysisRunResultId left, AnalysisRunResultId right) => left.Equals(right);
    public static bool operator !=(AnalysisRunResultId left, AnalysisRunResultId right) => !left.Equals(right);
}

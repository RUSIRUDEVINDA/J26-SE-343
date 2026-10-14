namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly struct AnalysisCapabilityCode : IEquatable<AnalysisCapabilityCode>
{
    public string Value { get; }
    
    public AnalysisCapabilityCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidAnalysisRunException("AnalysisCapabilityCode cannot be null or whitespace.");
            
        var trimmed = value.Trim();
        if (trimmed.Length > 100)
            throw new InvalidAnalysisRunException("AnalysisCapabilityCode exceeds maximum length of 100.");
            
        foreach (char c in trimmed)
        {
            if (char.IsControl(c))
                throw new InvalidAnalysisRunException("AnalysisCapabilityCode cannot contain control characters.");
        }
            
        Value = trimmed;
    }
    
    public bool Equals(AnalysisCapabilityCode other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => obj is AnalysisCapabilityCode other && Equals(other);
    public override int GetHashCode() => Value != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Value) : 0;
    
    public static bool operator ==(AnalysisCapabilityCode left, AnalysisCapabilityCode right) => left.Equals(right);
    public static bool operator !=(AnalysisCapabilityCode left, AnalysisCapabilityCode right) => !left.Equals(right);
    
    public override string ToString() => Value;
}

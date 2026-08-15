namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisRunFailure : IEquatable<AnalysisRunFailure>
{
    public string Code { get; }
    public string Description { get; }

    public AnalysisRunFailure(string code, string description)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidAnalysisRunTransitionException("Failure code cannot be blank.");
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidAnalysisRunTransitionException("Failure description cannot be blank.");

        var trimmedCode = code.Trim();
        var trimmedDescription = description.Trim();

        if (trimmedCode.Length > 100) throw new InvalidAnalysisRunTransitionException("Failure code exceeds maximum length of 100.");
        if (trimmedDescription.Length > 500) throw new InvalidAnalysisRunTransitionException("Failure description exceeds maximum length of 500.");

        foreach (char c in trimmedCode)
        {
            if (char.IsControl(c)) throw new InvalidAnalysisRunTransitionException("Failure code contains control characters.");
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                throw new InvalidAnalysisRunTransitionException("Failure code contains invalid characters.");
        }

        foreach (char c in trimmedDescription)
        {
            if (char.IsControl(c)) throw new InvalidAnalysisRunTransitionException("Failure description contains control characters.");
        }

        Code = trimmedCode;
        Description = trimmedDescription;
    }

    public bool Equals(AnalysisRunFailure? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Code, other.Code, StringComparison.Ordinal) && 
               string.Equals(Description, other.Description, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is AnalysisRunFailure other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Code, Description);
}

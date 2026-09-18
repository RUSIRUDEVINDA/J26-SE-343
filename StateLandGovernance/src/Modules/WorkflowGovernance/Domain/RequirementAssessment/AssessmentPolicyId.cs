namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct AssessmentPolicyId : IEquatable<AssessmentPolicyId>
{
    public string Value { get; }

    public AssessmentPolicyId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidAssessmentPolicyException("AssessmentPolicyId cannot be null, empty, or whitespace.");
        }

        Value = value.Trim();
    }

    public bool Equals(AssessmentPolicyId other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    public override string ToString() => Value ?? string.Empty;
}

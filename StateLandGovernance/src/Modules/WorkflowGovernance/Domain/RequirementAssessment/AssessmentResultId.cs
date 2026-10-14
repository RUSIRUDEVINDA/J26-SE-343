namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct AssessmentResultId : IEquatable<AssessmentResultId>
{
    public Guid Value { get; }

    public AssessmentResultId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidRequirementAssessmentException("AssessmentResultId cannot be empty.");
        }

        Value = value;
    }

    public static AssessmentResultId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

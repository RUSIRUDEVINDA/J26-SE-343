namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct ProposalContentAssessmentResultId
{
    public Guid Value { get; }

    public ProposalContentAssessmentResultId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidProposalContentAssessmentException("ProposalContentAssessmentResultId cannot be empty.");
        }

        Value = value;
    }

    public static ProposalContentAssessmentResultId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

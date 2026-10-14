namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct ProposalRequirementId
{
    public string Value { get; }

    public ProposalRequirementId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidProposalContentRequirementException("ProposalRequirementId cannot be null, empty, or whitespace.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

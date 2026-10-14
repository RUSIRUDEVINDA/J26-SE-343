namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct ProposalTemplateId
{
    public string Value { get; }

    public ProposalTemplateId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidProposalTemplateException("ProposalTemplateId cannot be null, empty, or whitespace.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

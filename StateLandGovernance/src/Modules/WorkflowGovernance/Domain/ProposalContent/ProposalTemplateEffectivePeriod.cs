namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record ProposalTemplateEffectivePeriod
{
    public DateTime? ValidFromUtc { get; }
    public DateTime? ValidToUtc { get; }

    public ProposalTemplateEffectivePeriod(DateTime? validFromUtc, DateTime? validToUtc)
    {
        if (validFromUtc.HasValue && validFromUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalTemplateException("ValidFromUtc must be UTC.");
        }

        if (validToUtc.HasValue && validToUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalTemplateException("ValidToUtc must be UTC.");
        }

        if (validFromUtc.HasValue && validToUtc.HasValue && validFromUtc.Value > validToUtc.Value)
        {
            throw new InvalidProposalTemplateException("ValidFromUtc cannot be after ValidToUtc.");
        }

        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
    }

    public bool IsEffectiveAt(DateTime timestampUtc)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalTemplateException("Evaluation timestamp must be UTC.");
        }

        if (ValidFromUtc.HasValue && timestampUtc < ValidFromUtc.Value) return false;
        if (ValidToUtc.HasValue && timestampUtc > ValidToUtc.Value) return false;
        return true;
    }
}

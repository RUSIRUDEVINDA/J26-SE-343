namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record EffectivePeriod
{
    public DateTime? ValidFromUtc { get; }
    public DateTime? ValidToUtc { get; }

    public EffectivePeriod(DateTime? validFromUtc, DateTime? validToUtc)
    {
        if (validFromUtc.HasValue && validFromUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidAssessmentPolicyException("ValidFromUtc must be UTC.");
        }

        if (validToUtc.HasValue && validToUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidAssessmentPolicyException("ValidToUtc must be UTC.");
        }

        if (validFromUtc.HasValue && validToUtc.HasValue && validFromUtc.Value > validToUtc.Value)
        {
            throw new InvalidAssessmentPolicyException("ValidFromUtc cannot be after ValidToUtc.");
        }

        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
    }

    public bool IsEffectiveAt(DateTime timestampUtc)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidAssessmentPolicyException("Evaluation timestamp must be UTC.");
        }

        if (ValidFromUtc.HasValue && timestampUtc < ValidFromUtc.Value) return false;
        if (ValidToUtc.HasValue && timestampUtc > ValidToUtc.Value) return false;
        return true;
    }
}

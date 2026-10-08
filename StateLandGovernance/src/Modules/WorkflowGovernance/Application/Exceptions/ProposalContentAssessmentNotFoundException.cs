namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class ProposalContentAssessmentNotFoundException : Exception
{
    public Guid AssessmentResultId { get; }

    public ProposalContentAssessmentNotFoundException(Guid assessmentResultId)
        : base($"ProposalContentCompletenessResult '{assessmentResultId}' was not found.")
    {
        AssessmentResultId = assessmentResultId;
    }
}

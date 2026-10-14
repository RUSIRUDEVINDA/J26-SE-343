namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class DocumentCompletenessAssessmentNotFoundException : Exception
{
    public Guid AssessmentId { get; }

    public DocumentCompletenessAssessmentNotFoundException(Guid assessmentId)
        : base($"DocumentCompletenessAssessment '{assessmentId}' was not found.")
    {
        AssessmentId = assessmentId;
    }
}

namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class DocumentCompletenessConcurrencyException : Exception
{
    public Guid AssessmentId { get; }
    public int ExpectedRevision { get; }
    public int ActualRevision { get; }

    public DocumentCompletenessConcurrencyException(Guid assessmentId, int expectedRevision, int actualRevision)
        : base($"DocumentCompletenessAssessment '{assessmentId}' revision mismatch. Expected {expectedRevision} but found {actualRevision}.")
    {
        AssessmentId = assessmentId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }
}

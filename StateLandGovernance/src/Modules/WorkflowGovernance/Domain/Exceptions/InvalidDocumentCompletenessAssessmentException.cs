namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class InvalidDocumentCompletenessAssessmentException : WorkflowGovernanceDomainException
{
    public InvalidDocumentCompletenessAssessmentException(string message) : base(message) { }
}

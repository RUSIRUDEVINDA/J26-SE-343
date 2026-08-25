namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class ConflictingClassificationReviewException : WorkflowGovernanceDomainException
{
    public ConflictingClassificationReviewException(string message) : base(message) { }
}

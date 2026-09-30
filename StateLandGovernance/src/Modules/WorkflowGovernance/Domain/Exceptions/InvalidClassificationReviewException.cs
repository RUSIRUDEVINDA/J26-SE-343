namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class InvalidClassificationReviewException : WorkflowGovernanceDomainException
{
    public InvalidClassificationReviewException(string message) : base(message) { }
}

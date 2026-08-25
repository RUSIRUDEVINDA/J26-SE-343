namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class DuplicateClassificationReviewException : WorkflowGovernanceDomainException
{
    public DuplicateClassificationReviewException(string message) : base(message) { }
}

namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class ClassificationAlreadyReviewedException : WorkflowGovernanceDomainException
{
    public ClassificationAlreadyReviewedException(string message) : base(message) { }
}

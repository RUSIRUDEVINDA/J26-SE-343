namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class DocumentCompletenessRevisionOverflowException : WorkflowGovernanceDomainException
{
    public DocumentCompletenessRevisionOverflowException(string message) : base(message) { }
}

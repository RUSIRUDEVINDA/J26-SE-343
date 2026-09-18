namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidMeasurementUnitException : WorkflowGovernanceDomainException
{
    public InvalidMeasurementUnitException(string message) : base(message)
    {
    }
}

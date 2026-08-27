using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.Exceptions;

public abstract class LeaseFeasibilityDomainException : Exception
{
    protected LeaseFeasibilityDomainException(string message)
        : base(message)
    {
    }

    protected LeaseFeasibilityDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

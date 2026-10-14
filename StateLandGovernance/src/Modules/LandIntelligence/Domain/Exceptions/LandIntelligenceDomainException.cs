namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public abstract class LandIntelligenceDomainException : Exception
{
    protected LandIntelligenceDomainException(string message)
        : base(message)
    {
    }

    protected LandIntelligenceDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public sealed class LeaseInterestInvalidStateException : LandIntelligenceDomainException
{
    public LeaseInterestInvalidStateException(string message)
        : base(message)
    {
    }
}

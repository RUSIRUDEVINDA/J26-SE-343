namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public sealed class LandIntelligenceAuthorizationException : LandIntelligenceDomainException
{
    public LandIntelligenceAuthorizationException(string message)
        : base(message)
    {
    }
}

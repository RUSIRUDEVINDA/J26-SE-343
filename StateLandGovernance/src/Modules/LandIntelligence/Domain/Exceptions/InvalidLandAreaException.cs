namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public sealed class InvalidLandAreaException : LandIntelligenceDomainException
{
    public InvalidLandAreaException(string message)
        : base(message)
    {
    }
}

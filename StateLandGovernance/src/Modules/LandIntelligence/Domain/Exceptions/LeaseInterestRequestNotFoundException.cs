namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public sealed class LeaseInterestRequestNotFoundException : LandIntelligenceDomainException
{
    public LeaseInterestRequestNotFoundException(Guid requestId)
        : base($"Lease interest request '{requestId}' was not found.")
    {
    }
}

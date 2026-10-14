namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public sealed class LandParcelNotFoundException : LandIntelligenceDomainException
{
    public Guid LandParcelId { get; }

    public LandParcelNotFoundException(Guid landParcelId)
        : base($"Land parcel '{landParcelId}' was not found.")
    {
        LandParcelId = landParcelId;
    }
}

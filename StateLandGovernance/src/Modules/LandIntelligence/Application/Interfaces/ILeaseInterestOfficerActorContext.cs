namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestOfficerActorContext
{
    Guid? OfficerActorId { get; }

    bool IsAuthenticated => OfficerActorId is { } id && id != Guid.Empty;
}

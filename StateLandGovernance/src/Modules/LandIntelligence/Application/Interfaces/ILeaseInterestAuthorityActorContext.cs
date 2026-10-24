namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestAuthorityActorContext
{
    Guid? AuthorityActorId { get; }

    bool IsAuthenticated => AuthorityActorId is { } id && id != Guid.Empty;
}

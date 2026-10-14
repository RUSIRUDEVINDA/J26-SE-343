namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Authenticated citizen identity for owner-scoped Land Intelligence operations.
/// Owner id is resolved from trusted server-side context (never from request body).
/// </summary>
public interface ILandIntelligenceCallerContext
{
    Guid? OwnerId { get; }

    bool IsAuthenticated => OwnerId is { } id && id != Guid.Empty;
}

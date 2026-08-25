namespace StateLandGovernance.LandIntelligence.Domain.Exceptions;

public sealed class LandRecommendationNotFoundException : LandIntelligenceDomainException
{
    public Guid RecommendationId { get; }

    public LandRecommendationNotFoundException(Guid recommendationId)
        : base($"Land recommendation '{recommendationId}' was not found.")
    {
        RecommendationId = recommendationId;
    }
}

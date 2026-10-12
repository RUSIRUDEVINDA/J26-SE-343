namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class ParcelShortlistItemEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid LandParcelId { get; set; }

    public DateTimeOffset SavedAtUtc { get; set; }
}

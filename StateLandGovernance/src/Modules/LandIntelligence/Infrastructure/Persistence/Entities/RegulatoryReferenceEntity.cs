namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class RegulatoryReferenceEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public LandParcelEntity LandParcel { get; set; } = null!;

    public string GazetteNumber { get; set; } = null!;

    public string Title { get; set; } = null!;

    public DateOnly EffectiveDate { get; set; }

    public string? Summary { get; set; }
}

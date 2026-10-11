namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class RealParcelSourceEntity
{
    public Guid LandParcelId { get; set; }

    public string? LandUseCode { get; set; }

    public string? LandUseEnglishLabel { get; set; }

    public string? LandUseDescription { get; set; }

    public string? ClaimantCategoryCode { get; set; }

    public string? ClaimantCategoryEnglishLabel { get; set; }
}

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class ColomboStateResearchProjectLeaseReadyEntity
{
    public string ResearchRecordId { get; set; } = string.Empty;

    public Guid LandParcelId { get; set; }

    public string DatasetKey { get; set; } = string.Empty;

    public string DesignationLabel { get; set; } = string.Empty;

    public string DesignationNotice { get; set; } = string.Empty;

    public DateTimeOffset ImportedAtUtc { get; set; }
}

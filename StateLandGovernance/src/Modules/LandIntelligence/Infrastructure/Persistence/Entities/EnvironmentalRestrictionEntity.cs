using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class EnvironmentalRestrictionEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public LandParcelEntity LandParcel { get; set; } = null!;

    public EnvironmentalRestrictionType Type { get; set; }

    public string Description { get; set; } = null!;

    public RestrictionSeverity Severity { get; set; }

    public string? DataProvenanceJson { get; set; }
}

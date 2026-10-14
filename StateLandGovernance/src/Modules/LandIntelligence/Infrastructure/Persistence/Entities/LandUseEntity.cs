using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class LandUseEntity
{
    public Guid Id { get; set; }

    public LandUseType Type { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public ICollection<LandParcelEntity> LandParcels { get; set; } = [];
}

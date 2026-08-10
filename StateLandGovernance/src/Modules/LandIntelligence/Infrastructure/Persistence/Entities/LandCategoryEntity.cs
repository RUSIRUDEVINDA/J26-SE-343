using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class LandCategoryEntity
{
    public Guid Id { get; set; }

    public LandCategoryType Type { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public ICollection<LandParcelEntity> LandParcels { get; set; } = [];
}

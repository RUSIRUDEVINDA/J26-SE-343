using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LandUseConfiguration : IEntityTypeConfiguration<LandUseEntity>
{
    public void Configure(EntityTypeBuilder<LandUseEntity> builder)
    {
        builder.ToTable("land_uses");

        builder.HasKey(landUse => landUse.Id);

        builder.Property(landUse => landUse.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(landUse => landUse.Description)
            .HasMaxLength(500);

        builder.HasIndex(landUse => landUse.Type)
            .IsUnique();

        builder.HasData(LandIntelligenceSeedData.LandUses);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LandCategoryConfiguration : IEntityTypeConfiguration<LandCategoryEntity>
{
    public void Configure(EntityTypeBuilder<LandCategoryEntity> builder)
    {
        builder.ToTable("land_categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(500);

        builder.HasIndex(category => category.Type)
            .IsUnique();

        builder.HasData(LandIntelligenceSeedData.Categories);
    }
}

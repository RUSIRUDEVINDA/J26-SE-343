using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class RealParcelSourceEntityConfiguration : IEntityTypeConfiguration<RealParcelSourceEntity>
{
    public void Configure(EntityTypeBuilder<RealParcelSourceEntity> builder)
    {
        builder.ToTable("real_parcel_sources", LandIntelligenceDbContext.SchemaName);
        builder.HasKey(entity => entity.LandParcelId);
        builder.Property(entity => entity.LandUseCode).HasMaxLength(100);
        builder.Property(entity => entity.LandUseEnglishLabel).HasMaxLength(200);
        builder.Property(entity => entity.LandUseDescription).HasMaxLength(300);
        builder.Property(entity => entity.ClaimantCategoryCode).HasMaxLength(100);
        builder.Property(entity => entity.ClaimantCategoryEnglishLabel).HasMaxLength(200);
    }
}

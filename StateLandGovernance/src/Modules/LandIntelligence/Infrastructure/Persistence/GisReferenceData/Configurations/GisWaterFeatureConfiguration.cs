using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal sealed class GisWaterFeatureConfiguration : IEntityTypeConfiguration<GisWaterFeatureEntity>
{
    public void Configure(EntityTypeBuilder<GisWaterFeatureEntity> builder)
    {
        builder.ConfigureGisReferenceTable("gis_water_features");

        builder.HasKey(entity => entity.Id);

        builder.ConfigureGisReferenceProvenance();

        builder.Property(entity => entity.Name)
            .HasMaxLength(200);

        builder.Property(entity => entity.Geometry)
            .HasColumnType("geometry (Geometry, 4326)")
            .IsRequired();

        builder.HasIndex(entity => entity.Geometry)
            .HasMethod("GIST");

        builder.HasIndex(entity => entity.FeatureType);
    }
}

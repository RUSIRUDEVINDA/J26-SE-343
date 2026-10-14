using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal sealed class GisSoilErosionObservationConfiguration
    : IEntityTypeConfiguration<GisSoilErosionObservationEntity>
{
    public void Configure(EntityTypeBuilder<GisSoilErosionObservationEntity> builder)
    {
        builder.ConfigureGisReferenceTable("gis_soil_erosion_observations");

        builder.HasKey(entity => entity.Id);

        builder.ConfigureGisReferenceProvenance();

        builder.Property(entity => entity.ObservationClass)
            .HasMaxLength(100);

        builder.Property(entity => entity.Description)
            .HasMaxLength(1000);

        builder.Property(entity => entity.ErosionRate)
            .HasPrecision(10, 4);

        builder.Property(entity => entity.Location)
            .HasColumnType("geometry (Point, 4326)")
            .IsRequired();

        builder.HasIndex(entity => entity.Location)
            .HasMethod("GIST");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal sealed class GisSoilConservationAreaConfiguration
    : IEntityTypeConfiguration<GisSoilConservationAreaEntity>
{
    public void Configure(EntityTypeBuilder<GisSoilConservationAreaEntity> builder)
    {
        builder.ConfigureGisReferenceTable("gis_soil_conservation_areas");

        builder.HasKey(entity => entity.Id);

        builder.ConfigureGisReferenceProvenance();

        builder.Property(entity => entity.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasMaxLength(1000);

        builder.Property(entity => entity.Boundary)
            .HasColumnType("geometry (MultiPolygon, 4326)")
            .IsRequired();

        builder.HasIndex(entity => entity.Boundary)
            .HasMethod("GIST");
    }
}

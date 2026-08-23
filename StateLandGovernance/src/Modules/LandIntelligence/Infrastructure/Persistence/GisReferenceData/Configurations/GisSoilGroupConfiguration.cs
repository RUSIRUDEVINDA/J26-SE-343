using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal sealed class GisSoilGroupConfiguration : IEntityTypeConfiguration<GisSoilGroupEntity>
{
    public void Configure(EntityTypeBuilder<GisSoilGroupEntity> builder)
    {
        builder.ConfigureGisReferenceTable("gis_soil_groups");

        builder.HasKey(entity => entity.Id);

        builder.ConfigureGisReferenceProvenance();

        builder.Property(entity => entity.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Boundary)
            .HasColumnType("geometry (MultiPolygon, 4326)")
            .IsRequired();

        builder.HasIndex(entity => entity.Boundary)
            .HasMethod("GIST");
    }
}

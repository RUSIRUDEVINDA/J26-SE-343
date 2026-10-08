using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal sealed class GisAdministrativeBoundaryConfiguration
    : IEntityTypeConfiguration<GisAdministrativeBoundaryEntity>
{
    public void Configure(EntityTypeBuilder<GisAdministrativeBoundaryEntity> builder)
    {
        builder.ConfigureGisReferenceTable("gis_administrative_boundaries");

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

        builder.HasIndex(entity => entity.BoundaryType);
    }
}

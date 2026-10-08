using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal sealed class GisRoadConfiguration : IEntityTypeConfiguration<GisRoadEntity>
{
    public void Configure(EntityTypeBuilder<GisRoadEntity> builder)
    {
        builder.ConfigureGisReferenceTable("gis_roads");

        builder.HasKey(entity => entity.Id);

        builder.ConfigureGisReferenceProvenance();

        builder.Property(entity => entity.Name)
            .HasMaxLength(200);

        builder.Property(entity => entity.Geometry)
            .HasColumnType("geometry (MultiLineString, 4326)")
            .IsRequired();

        builder.HasIndex(entity => entity.Geometry)
            .HasMethod("GIST");

        builder.HasIndex(entity => entity.RoadType);
    }
}

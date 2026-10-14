using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LandParcelGisEnrichmentSnapshotConfiguration
    : IEntityTypeConfiguration<LandParcelGisEnrichmentSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<LandParcelGisEnrichmentSnapshotEntity> builder)
    {
        builder.ToTable("land_parcel_gis_enrichment_snapshots");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.DetectedProvince)
            .HasMaxLength(100);

        builder.Property(entity => entity.DetectedDistrict)
            .HasMaxLength(100);

        builder.Property(entity => entity.SourceName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(entity => entity.LandParcelId)
            .IsUnique();

        builder.HasOne(entity => entity.LandParcel)
            .WithOne(parcel => parcel.GisEnrichmentSnapshot)
            .HasForeignKey<LandParcelGisEnrichmentSnapshotEntity>(entity => entity.LandParcelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

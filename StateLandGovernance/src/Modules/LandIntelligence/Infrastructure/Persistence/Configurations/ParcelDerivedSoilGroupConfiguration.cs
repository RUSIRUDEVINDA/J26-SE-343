using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ParcelDerivedSoilGroupConfiguration : IEntityTypeConfiguration<ParcelDerivedSoilGroupEntity>
{
    public void Configure(EntityTypeBuilder<ParcelDerivedSoilGroupEntity> builder)
    {
        builder.ToTable("parcel_derived_soil_groups");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.SoilGroupName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.OverlapAreaSquareMeters)
            .HasPrecision(18, 4);

        builder.Property(entity => entity.OverlapPercentage)
            .HasPrecision(8, 4);

        builder.Property(entity => entity.SourceName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.SourceLayer)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.ProvenanceJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(entity => entity.LandParcelId)
            .IsUnique();

        builder.HasOne(entity => entity.LandParcel)
            .WithOne(parcel => parcel.DerivedSoilGroup)
            .HasForeignKey<ParcelDerivedSoilGroupEntity>(entity => entity.LandParcelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

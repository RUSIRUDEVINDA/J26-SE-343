using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class RegulatoryReferenceConfiguration : IEntityTypeConfiguration<RegulatoryReferenceEntity>
{
    public void Configure(EntityTypeBuilder<RegulatoryReferenceEntity> builder)
    {
        builder.ToTable("regulatory_references");

        builder.HasKey(reference => reference.Id);

        builder.Property(reference => reference.GazetteNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(reference => reference.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(reference => reference.EffectiveDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(reference => reference.Summary)
            .HasMaxLength(1000);

        builder.HasIndex(reference => reference.LandParcelId);

        builder.HasOne(reference => reference.LandParcel)
            .WithMany(parcel => parcel.RegulatoryReferences)
            .HasForeignKey(reference => reference.LandParcelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

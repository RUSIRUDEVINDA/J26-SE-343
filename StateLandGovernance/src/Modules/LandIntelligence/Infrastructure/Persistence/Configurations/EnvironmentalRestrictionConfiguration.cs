using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class EnvironmentalRestrictionConfiguration : IEntityTypeConfiguration<EnvironmentalRestrictionEntity>
{
    public void Configure(EntityTypeBuilder<EnvironmentalRestrictionEntity> builder)
    {
        builder.ToTable("environmental_restrictions");

        builder.HasKey(restriction => restriction.Id);

        builder.Property(restriction => restriction.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(restriction => restriction.DataProvenanceJson)
            .HasColumnType("jsonb");

        builder.HasIndex(restriction => restriction.LandParcelId);

        builder.HasOne(restriction => restriction.LandParcel)
            .WithMany(parcel => parcel.EnvironmentalRestrictions)
            .HasForeignKey(restriction => restriction.LandParcelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

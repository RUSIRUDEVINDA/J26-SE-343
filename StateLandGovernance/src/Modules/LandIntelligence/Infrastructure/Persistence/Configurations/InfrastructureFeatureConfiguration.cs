using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class InfrastructureFeatureConfiguration : IEntityTypeConfiguration<InfrastructureFeatureEntity>
{
    public void Configure(EntityTypeBuilder<InfrastructureFeatureEntity> builder)
    {
        builder.ToTable("infrastructure_features");

        builder.HasKey(feature => feature.Id);

        builder.Property(feature => feature.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(feature => feature.Description)
            .HasMaxLength(500);

        builder.Property(feature => feature.DistanceMeters)
            .HasPrecision(12, 2);

        builder.Property(feature => feature.Location)
            .HasColumnType("geometry (Point, 4326)");

        builder.HasIndex(feature => feature.Location)
            .HasMethod("GIST");

        builder.HasOne(feature => feature.LandParcel)
            .WithMany(parcel => parcel.InfrastructureFeatures)
            .HasForeignKey(feature => feature.LandParcelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

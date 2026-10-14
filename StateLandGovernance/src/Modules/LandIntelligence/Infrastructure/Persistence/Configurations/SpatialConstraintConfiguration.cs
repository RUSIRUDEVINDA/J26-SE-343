using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class SpatialConstraintConfiguration : IEntityTypeConfiguration<SpatialConstraintEntity>
{
    public void Configure(EntityTypeBuilder<SpatialConstraintEntity> builder)
    {
        builder.ToTable("spatial_constraints");

        builder.HasKey(constraint => constraint.Id);

        builder.Property(constraint => constraint.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(constraint => constraint.ConstraintGeometry)
            .HasColumnType("geometry (MultiPolygon, 4326)");

        builder.HasIndex(constraint => constraint.ConstraintGeometry)
            .HasMethod("GIST");

        builder.HasOne(constraint => constraint.LandParcel)
            .WithMany(parcel => parcel.SpatialConstraints)
            .HasForeignKey(constraint => constraint.LandParcelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

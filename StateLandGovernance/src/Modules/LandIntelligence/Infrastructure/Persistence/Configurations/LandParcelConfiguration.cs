using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LandParcelConfiguration : IEntityTypeConfiguration<LandParcelEntity>
{
    public void Configure(EntityTypeBuilder<LandParcelEntity> builder)
    {
        builder.ToTable("land_parcels");

        builder.HasKey(parcel => parcel.Id);

        builder.Property(parcel => parcel.CadastralNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(parcel => parcel.SurveyPlanReference)
            .HasMaxLength(100);

        builder.Property(parcel => parcel.Province)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(parcel => parcel.District)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(parcel => parcel.DivisionalSecretariat)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(parcel => parcel.GramaNiladhariDivision)
            .HasMaxLength(150);

        builder.Property(parcel => parcel.AreaValue)
            .HasPrecision(18, 4);

        builder.Property(parcel => parcel.SoilType)
            .HasMaxLength(100);

        builder.Property(parcel => parcel.TerrainDescription)
            .HasMaxLength(500);

        builder.Property(parcel => parcel.ElevationMeters)
            .HasPrecision(10, 2);

        builder.Property(parcel => parcel.Centroid)
            .HasColumnType("geometry (Point, 4326)")
            .IsRequired();

        builder.Property(parcel => parcel.Boundary)
            .HasColumnType("geometry (MultiPolygon, 4326)");

        builder.HasIndex(parcel => parcel.CadastralNumber)
            .IsUnique();

        builder.HasIndex(parcel => parcel.Centroid)
            .HasMethod("GIST");

        builder.HasIndex(parcel => parcel.Boundary)
            .HasMethod("GIST");

        builder.HasOne(parcel => parcel.LandCategory)
            .WithMany(category => category.LandParcels)
            .HasForeignKey(parcel => parcel.LandCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(parcel => parcel.CurrentLandUse)
            .WithMany(landUse => landUse.LandParcels)
            .HasForeignKey(parcel => parcel.CurrentLandUseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

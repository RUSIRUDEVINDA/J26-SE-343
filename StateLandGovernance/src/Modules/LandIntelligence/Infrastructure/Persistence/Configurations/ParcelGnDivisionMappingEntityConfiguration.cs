using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ParcelGnDivisionMappingEntityConfiguration
    : IEntityTypeConfiguration<ParcelGnDivisionMappingEntity>
{
    public void Configure(EntityTypeBuilder<ParcelGnDivisionMappingEntity> builder)
    {
        builder.ToTable("parcel_gn_division_mappings", LandIntelligenceDbContext.SchemaName);
        builder.HasKey(entity => entity.CadastralNumber);
        builder.Property(entity => entity.CadastralNumber).HasMaxLength(64);
        builder.Property(entity => entity.DatasetKey).HasMaxLength(80);
        builder.Property(entity => entity.SourceGnIndex).HasMaxLength(32);
        builder.Property(entity => entity.WorkbookStatusLabel).HasMaxLength(120);
        builder.Property(entity => entity.SourceCodeGnName).HasMaxLength(200);
        builder.Property(entity => entity.SourceCodeGnNumber).HasMaxLength(32);
        builder.Property(entity => entity.SourceCodeDs).HasMaxLength(120);
        builder.Property(entity => entity.CodeDistrict).HasMaxLength(80);
        builder.Property(entity => entity.MarkerGnName).HasMaxLength(200);
        builder.Property(entity => entity.MarkerGnNumber).HasMaxLength(32);
        builder.Property(entity => entity.MarkerDs).HasMaxLength(120);
        builder.Property(entity => entity.WorkbookSha256).HasMaxLength(64);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(entity => entity.LandParcelId).IsUnique();
    }
}

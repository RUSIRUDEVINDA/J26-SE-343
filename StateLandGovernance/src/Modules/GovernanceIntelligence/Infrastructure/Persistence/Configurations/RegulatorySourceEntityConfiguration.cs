using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class RegulatorySourceEntityConfiguration : IEntityTypeConfiguration<RegulatorySourceEntity>
{
    public void Configure(EntityTypeBuilder<RegulatorySourceEntity> builder)
    {
        builder.ToTable("regulatory_sources", "governance_intelligence");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.SourceType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Authority)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.DocumentTitle)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(s => s.DocumentVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.GazetteNumber)
            .HasMaxLength(100);

        builder.Property(s => s.PublishedDate)
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.EffectiveFrom)
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.EffectiveTo)
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.DocumentReference)
            .HasMaxLength(500);

        builder.Property(s => s.Checksum)
            .HasMaxLength(100);

        builder.Property(s => s.IsActive)
            .IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ColomboStateResearchProjectLeaseReadyEntityConfiguration
    : IEntityTypeConfiguration<ColomboStateResearchProjectLeaseReadyEntity>
{
    public void Configure(EntityTypeBuilder<ColomboStateResearchProjectLeaseReadyEntity> builder)
    {
        builder.ToTable("colombo_state_research_project_lease_ready");

        builder.HasKey(x => x.ResearchRecordId);

        builder.Property(x => x.ResearchRecordId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.LandParcelId).IsRequired();
        builder.Property(x => x.DatasetKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DesignationLabel).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DesignationNotice).IsRequired();
        builder.Property(x => x.ImportedAtUtc).IsRequired();

        builder.HasIndex(x => x.LandParcelId).IsUnique();
        builder.HasIndex(x => x.DatasetKey);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration mapping GovernanceAuditRecordEntity to table governance_intelligence.governance_audit_records.
/// </summary>
public sealed class GovernanceAuditRecordEntityConfiguration : IEntityTypeConfiguration<GovernanceAuditRecordEntity>
{
    public void Configure(EntityTypeBuilder<GovernanceAuditRecordEntity> builder)
    {
        builder.ToTable("governance_audit_records", GovernanceIntelligenceDbContext.SchemaName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Details)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => x.Timestamp)
            .HasDatabaseName("idx_gov_audit_timestamp");

        builder.HasIndex(x => x.EngineType)
            .HasDatabaseName("idx_gov_audit_engine_type");

        builder.HasIndex(x => x.ActionName)
            .HasDatabaseName("idx_gov_audit_action_name");
    }
}

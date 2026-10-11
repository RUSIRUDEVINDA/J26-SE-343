using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration mapping GovernanceAuditAnchorOutboxEntity to table governance_intelligence.governance_audit_anchor_outbox.
/// </summary>
public sealed class GovernanceAuditAnchorOutboxEntityConfiguration : IEntityTypeConfiguration<GovernanceAuditAnchorOutboxEntity>
{
    public void Configure(EntityTypeBuilder<GovernanceAuditAnchorOutboxEntity> builder)
    {
        builder.ToTable("governance_audit_anchor_outbox", GovernanceIntelligenceDbContext.SchemaName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AuditRecordId)
            .IsRequired();

        builder.Property(x => x.RecordHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.EngineType)
            .IsRequired();

        builder.Property(x => x.RecordVersion)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .IsRequired();

        builder.Property(x => x.NextAttemptAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.LastErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc })
            .HasDatabaseName("idx_gov_anchor_outbox_status_next_attempt");

        builder.HasIndex(x => x.AuditRecordId)
            .HasDatabaseName("idx_gov_anchor_outbox_audit_record_id");
    }
}

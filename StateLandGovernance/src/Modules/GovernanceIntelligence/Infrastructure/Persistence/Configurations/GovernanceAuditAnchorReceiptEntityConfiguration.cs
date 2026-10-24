using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration mapping GovernanceAuditAnchorReceiptEntity to table governance_intelligence.governance_audit_anchor_receipts.
/// </summary>
public sealed class GovernanceAuditAnchorReceiptEntityConfiguration : IEntityTypeConfiguration<GovernanceAuditAnchorReceiptEntity>
{
    public void Configure(EntityTypeBuilder<GovernanceAuditAnchorReceiptEntity> builder)
    {
        builder.ToTable("governance_audit_anchor_receipts", GovernanceIntelligenceDbContext.SchemaName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AuditRecordId)
            .IsRequired();

        builder.Property(x => x.RecordHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.AnchorStatus)
            .IsRequired();

        builder.Property(x => x.TransactionReference)
            .HasMaxLength(128);

        builder.Property(x => x.ContractAddress)
            .HasMaxLength(64);

        builder.Property(x => x.AnchoredAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastVerifiedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.VerificationStatus)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => x.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_gov_anchor_receipt_audit_record_id");

        builder.HasIndex(x => x.RecordHash)
            .HasDatabaseName("idx_gov_anchor_receipt_record_hash");

        builder.HasIndex(x => x.AnchorStatus)
            .HasDatabaseName("idx_gov_anchor_receipt_status");
    }
}

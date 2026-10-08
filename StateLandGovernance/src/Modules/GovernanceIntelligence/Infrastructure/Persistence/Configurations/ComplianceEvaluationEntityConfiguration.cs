using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class ComplianceEvaluationEntityConfiguration : IEntityTypeConfiguration<ComplianceEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<ComplianceEvaluationEntity> builder)
    {
        builder.ToTable("compliance_evaluations", "governance_intelligence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.ActionName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ProposalId)
            .HasMaxLength(100);

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.DeterministicEvaluationId)
            .HasMaxLength(100);

        builder.Property(e => e.RuleSetVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.FindingsJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.EvaluationTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_uniq_compliance_eval_audit_id");

        builder.HasIndex(e => e.ProposalId)
            .HasDatabaseName("idx_comp_eval_proposal_id");

        builder.HasIndex(e => e.DeterministicEvaluationId)
            .HasDatabaseName("idx_comp_eval_det_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_comp_eval_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

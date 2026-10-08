using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class ConflictEvaluationEntityConfiguration : IEntityTypeConfiguration<ConflictEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<ConflictEvaluationEntity> builder)
    {
        builder.ToTable("conflict_evaluations", "governance_intelligence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.ActionName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.HighestSeverity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EvaluationTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_uniq_conflict_eval_audit_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_conf_eval_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Findings)
            .WithOne(f => f.ConflictEvaluation)
            .HasForeignKey(f => f.ConflictEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConflictFindingEntityConfiguration : IEntityTypeConfiguration<ConflictFindingEntity>
{
    public void Configure(EntityTypeBuilder<ConflictFindingEntity> builder)
    {
        builder.ToTable("conflict_findings", "governance_intelligence");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        builder.Property(f => f.ConflictId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.ConflictType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Severity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.DetectionStatus)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.SubjectId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.InvolvedDecisionIdsJson)
            .IsRequired();

        builder.Property(f => f.InvolvedInstitutionsJson)
            .IsRequired();

        builder.Property(f => f.Explanation)
            .IsRequired();

        builder.Property(f => f.EvidenceRule)
            .IsRequired();

        builder.Property(f => f.RecommendedAction)
            .IsRequired();

        builder.Property(f => f.DetectionTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(f => f.OrderIndex)
            .IsRequired();

        builder.HasIndex(f => f.SubjectId)
            .HasDatabaseName("idx_conf_finding_subject_id");
    }
}

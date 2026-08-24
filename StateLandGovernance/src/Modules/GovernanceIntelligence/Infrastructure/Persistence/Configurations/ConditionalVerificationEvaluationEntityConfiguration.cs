using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class ConditionalVerificationEvaluationEntityConfiguration : IEntityTypeConfiguration<ConditionalVerificationEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<ConditionalVerificationEvaluationEntity> builder)
    {
        builder.ToTable("conditional_verification_evaluations", "governance_intelligence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.VerificationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SubjectId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Outcome)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SummaryExplanation)
            .IsRequired();

        builder.Property(e => e.RecommendedAction)
            .IsRequired();

        builder.Property(e => e.EvaluationTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_uniq_cond_verif_eval_audit_id");

        builder.HasIndex(e => e.SubjectId)
            .HasDatabaseName("idx_cond_verif_subject_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_cond_verif_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ConditionResults)
            .WithOne(r => r.ConditionalVerificationEvaluation)
            .HasForeignKey(r => r.ConditionalVerificationEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConditionResultEntityConfiguration : IEntityTypeConfiguration<ConditionResultEntity>
{
    public void Configure(EntityTypeBuilder<ConditionResultEntity> builder)
    {
        builder.ToTable("condition_results", "governance_intelligence");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.ConditionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.FailureReason)
            .IsRequired();

        builder.Property(r => r.OrderIndex)
            .IsRequired();
    }
}

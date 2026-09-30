using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class RiskEvaluationEntityConfiguration : IEntityTypeConfiguration<RiskEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<RiskEvaluationEntity> builder)
    {
        builder.ToTable("risk_evaluations", "governance_intelligence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.SubjectId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Severity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EvaluationTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_uniq_risk_eval_audit_id");

        builder.HasIndex(e => e.SubjectId)
            .HasDatabaseName("idx_risk_eval_subject_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_risk_eval_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.RiskIndicators)
            .WithOne(i => i.RiskEvaluation)
            .HasForeignKey(i => i.RiskEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RiskIndicatorEntityConfiguration : IEntityTypeConfiguration<RiskIndicatorEntity>
{
    public void Configure(EntityTypeBuilder<RiskIndicatorEntity> builder)
    {
        builder.ToTable("risk_indicators", "governance_intelligence");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.IndicatorId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Severity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.SubjectId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.InvolvedActorsJson)
            .IsRequired();

        builder.Property(i => i.TriggeredRule)
            .IsRequired();

        builder.Property(i => i.Explanation)
            .IsRequired();

        builder.Property(i => i.RecommendedAction)
            .IsRequired();

        builder.Property(i => i.OrderIndex)
            .IsRequired();

        builder.HasIndex(i => i.SubjectId)
            .HasDatabaseName("idx_risk_ind_subject_id");
    }
}

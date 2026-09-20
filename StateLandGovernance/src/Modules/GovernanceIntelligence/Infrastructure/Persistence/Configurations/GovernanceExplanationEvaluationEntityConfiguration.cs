using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class GovernanceExplanationEvaluationEntityConfiguration : IEntityTypeConfiguration<GovernanceExplanationEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<GovernanceExplanationEvaluationEntity> builder)
    {
        builder.ToTable("governance_explanations", "governance_intelligence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.ExplanationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SubjectId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.OverallSeverity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Disclaimer)
            .IsRequired();

        builder.Property(e => e.EvaluationTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_uniq_gov_expl_eval_audit_id");

        builder.HasIndex(e => e.SubjectId)
            .HasDatabaseName("idx_gov_expl_subject_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_gov_expl_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Items)
            .WithOne(i => i.ExplanationEvaluation)
            .HasForeignKey(i => i.GovernanceExplanationEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GovernanceExplanationItemEntityConfiguration : IEntityTypeConfiguration<GovernanceExplanationItemEntity>
{
    public void Configure(EntityTypeBuilder<GovernanceExplanationItemEntity> builder)
    {
        builder.ToTable("governance_explanation_items", "governance_intelligence");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.ItemId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.SourceEngine)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.OutcomeStatus)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.Severity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.ReasonCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.PlainLanguageExplanation)
            .IsRequired();

        builder.Property(i => i.RecommendedAction)
            .IsRequired();

        builder.Property(i => i.OrderIndex)
            .IsRequired();
    }
}

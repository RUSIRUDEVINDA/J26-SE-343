using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class ConsensusEvaluationEntityConfiguration : IEntityTypeConfiguration<ConsensusEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<ConsensusEvaluationEntity> builder)
    {
        builder.ToTable("consensus_evaluations", "governance_intelligence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.ConsensusEvaluationId)
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
            .HasDatabaseName("idx_uniq_consensus_eval_audit_id");

        builder.HasIndex(e => e.SubjectId)
            .HasDatabaseName("idx_cons_eval_subject_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_cons_eval_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.InstitutionPositions)
            .WithOne(p => p.ConsensusEvaluation)
            .HasForeignKey(p => p.ConsensusEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InstitutionPositionEntityConfiguration : IEntityTypeConfiguration<InstitutionPositionEntity>
{
    public void Configure(EntityTypeBuilder<InstitutionPositionEntity> builder)
    {
        builder.ToTable("institution_positions", "governance_intelligence");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.InstitutionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Position)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.AuthorityRole)
            .HasMaxLength(100);

        builder.Property(p => p.ReasonCode)
            .HasMaxLength(100);

        builder.Property(p => p.SubmittedTimestamp)
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.OrderIndex)
            .IsRequired();

        builder.HasIndex(p => p.InstitutionId)
            .HasDatabaseName("idx_inst_pos_institution_id");
    }
}

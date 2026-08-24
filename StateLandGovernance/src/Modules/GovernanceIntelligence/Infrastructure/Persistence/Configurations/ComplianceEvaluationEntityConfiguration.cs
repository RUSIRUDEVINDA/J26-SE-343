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

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EvaluationTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.AuditRecordId)
            .IsUnique()
            .HasDatabaseName("idx_uniq_compliance_eval_audit_id");

        builder.HasIndex(e => e.EvaluationTimestamp)
            .HasDatabaseName("idx_comp_eval_timestamp");

        builder.HasOne(e => e.AuditRecord)
            .WithMany()
            .HasForeignKey(e => e.AuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Violations)
            .WithOne(v => v.ComplianceEvaluation)
            .HasForeignKey(v => v.ComplianceEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Conditions)
            .WithOne(c => c.ComplianceEvaluation)
            .HasForeignKey(c => c.ComplianceEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComplianceViolationEntityConfiguration : IEntityTypeConfiguration<ComplianceViolationEntity>
{
    public void Configure(EntityTypeBuilder<ComplianceViolationEntity> builder)
    {
        builder.ToTable("compliance_violations", "governance_intelligence");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .ValueGeneratedNever();

        builder.Property(v => v.RuleCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.Message)
            .IsRequired();

        builder.Property(v => v.OrderIndex)
            .IsRequired();
    }
}

public class ComplianceConditionEntityConfiguration : IEntityTypeConfiguration<ComplianceConditionEntity>
{
    public void Configure(EntityTypeBuilder<ComplianceConditionEntity> builder)
    {
        builder.ToTable("compliance_conditions", "governance_intelligence");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Description)
            .IsRequired();

        builder.Property(c => c.RequiredByDate)
            .HasColumnType("timestamp with time zone");

        builder.Property(c => c.OrderIndex)
            .IsRequired();
    }
}

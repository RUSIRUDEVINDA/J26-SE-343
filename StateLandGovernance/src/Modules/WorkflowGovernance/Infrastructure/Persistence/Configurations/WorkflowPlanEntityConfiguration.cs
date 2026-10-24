namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class WorkflowPlanEntityConfiguration : IEntityTypeConfiguration<WorkflowPlanEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowPlanEntity> builder)
    {
        builder.ToTable("workflow_plans", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.LeaseCaseId)
            .HasColumnName("lease_case_id")
            .IsRequired();

        builder.Property(e => e.Source)
            .HasColumnName("source")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.VerifiedFactSnapshotId)
            .HasColumnName("verified_fact_snapshot_id")
            .IsRequired();

        builder.Property(e => e.DocumentCompletenessAssessmentId)
            .HasColumnName("document_completeness_assessment_id")
            .IsRequired();

        builder.Property(e => e.RuleSetReferenceJson)
            .HasColumnName("rule_set_reference_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RecommendationReferenceJson)
            .HasColumnName("recommendation_reference_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.ApprovedAt)
            .HasColumnName("approved_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.SupersededAt)
            .HasColumnName("superseded_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.SupersessionReason)
            .HasColumnName("supersession_reason")
            .HasMaxLength(500);

        builder.Property(e => e.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(e => e.StagesJson)
            .HasColumnName("stages_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.LeaseCaseId)
            .HasDatabaseName("ix_workflow_plans_lease_case_id");

        builder.HasOne(e => e.LeaseCase)
            .WithMany(c => c.WorkflowPlans)
            .HasForeignKey(e => e.LeaseCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

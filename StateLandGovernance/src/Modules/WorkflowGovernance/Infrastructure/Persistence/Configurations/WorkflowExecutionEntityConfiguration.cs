namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class WorkflowExecutionEntityConfiguration : IEntityTypeConfiguration<WorkflowExecutionEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExecutionEntity> builder)
    {
        builder.ToTable("workflow_executions", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.WorkflowPlanId)
            .HasColumnName("workflow_plan_id")
            .IsRequired();

        builder.Property(e => e.WorkflowPlanRevision)
            .HasColumnName("workflow_plan_revision")
            .IsRequired();

        builder.Property(e => e.LeaseCaseId)
            .HasColumnName("lease_case_id")
            .IsRequired();

        builder.Property(e => e.RuleSetReferenceJson)
            .HasColumnName("rule_set_reference_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(e => e.StagesJson)
            .HasColumnName("stages_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CompletedWorkflowJson)
            .HasColumnName("completed_workflow_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.WorkflowPlanId)
            .HasDatabaseName("ix_workflow_executions_workflow_plan_id");

        builder.HasIndex(e => e.LeaseCaseId)
            .HasDatabaseName("ix_workflow_executions_lease_case_id");

        builder.HasOne(e => e.LeaseCase)
            .WithMany(c => c.WorkflowExecutions)
            .HasForeignKey(e => e.LeaseCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

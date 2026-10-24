namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class LeaseCaseEntityConfiguration : IEntityTypeConfiguration<LeaseCaseEntity>
{
    public void Configure(EntityTypeBuilder<LeaseCaseEntity> builder)
    {
        builder.ToTable("lease_cases", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);
        // Fulfilment methods do not advance the frozen domain revision. PostgreSQL xmin
        // protects those updates too, without adding a physical column or changing Domain.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.ApplicationReference)
            .HasColumnName("application_reference")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.CreatedByActorId)
            .HasColumnName("created_by_actor_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CurrentVerifiedFactSnapshotId)
            .HasColumnName("current_verified_fact_snapshot_id")
            .IsRequired();

        builder.Property(e => e.ProposalIntakeJson)
            .HasColumnName("proposal_intake_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.LatestScreeningJson)
            .HasColumnName("latest_screening_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.ActiveWorkflowPlanId)
            .HasColumnName("active_workflow_plan_id");

        builder.Property(e => e.CompletedWorkflowJson)
            .HasColumnName("completed_workflow_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.HandoffPackageJson)
            .HasColumnName("handoff_package_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.ApprovalConditionsJson)
            .HasColumnName("approval_conditions_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.DocumentSubmissionRequirementsJson)
            .HasColumnName("document_submission_requirements_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.EscalationsJson)
            .HasColumnName("escalations_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.AssessmentHistoryJson)
            .HasColumnName("assessment_history_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ProposalContentAssessmentsJson)
            .HasColumnName("proposal_content_assessments_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.WorkflowPlanHistoryJson)
            .HasColumnName("workflow_plan_history_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ProposalContentSequenceCounter)
            .HasColumnName("proposal_content_sequence_counter")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Authoritative unique constraint on ApplicationReference
        builder.HasIndex(e => e.ApplicationReference)
            .IsUnique()
            .HasDatabaseName("ix_lease_cases_application_reference");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_lease_cases_status");
    }
}

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class DocumentCompletenessAssessmentEntityConfiguration : IEntityTypeConfiguration<DocumentCompletenessAssessmentEntity>
{
    public void Configure(EntityTypeBuilder<DocumentCompletenessAssessmentEntity> builder)
    {
        builder.ToTable("document_completeness_assessments", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.LeaseCaseId)
            .HasColumnName("lease_case_id")
            .IsRequired();

        builder.Property(e => e.RequirementSetIdentifier)
            .HasColumnName("requirement_set_identifier")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.RequirementSetVersion)
            .HasColumnName("requirement_set_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.AssessedAt)
            .HasColumnName("assessed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(e => e.AssessedDocumentsJson)
            .HasColumnName("assessed_documents_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ClassifiedDocumentsJson)
            .HasColumnName("classified_documents_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RequirementsJson)
            .HasColumnName("requirements_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.ReviewsJson)
            .HasColumnName("reviews_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.MissingRequirementsJson)
            .HasColumnName("missing_requirements_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.LeaseCaseId)
            .HasDatabaseName("ix_doc_completeness_assessments_lease_case_id");

        builder.HasOne(e => e.LeaseCase)
            .WithMany(c => c.CompletenessAssessments)
            .HasForeignKey(e => e.LeaseCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

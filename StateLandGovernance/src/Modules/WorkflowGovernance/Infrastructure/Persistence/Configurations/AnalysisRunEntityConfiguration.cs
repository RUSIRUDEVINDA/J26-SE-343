namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class AnalysisRunEntityConfiguration : IEntityTypeConfiguration<AnalysisRunEntity>
{
    public void Configure(EntityTypeBuilder<AnalysisRunEntity> builder)
    {
        builder.ToTable("analysis_runs", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.DocumentAnalysisId)
            .HasColumnName("document_analysis_id")
            .IsRequired();

        builder.Property(e => e.RunNumber)
            .HasColumnName("run_number")
            .IsRequired();

        builder.Property(e => e.DocumentVersionId)
            .HasColumnName("document_version_id")
            .IsRequired();

        builder.Property(e => e.ChecksumAlgorithm)
            .HasColumnName("checksum_algorithm")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ChecksumValue)
            .HasColumnName("checksum_value")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.ModelProvider)
            .HasColumnName("model_provider")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ModelName)
            .HasColumnName("model_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ModelVersion)
            .HasColumnName("model_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.RequestedCapabilitiesJson)
            .HasColumnName("requested_capabilities_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RequestedAt)
            .HasColumnName("requested_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.State)
            .HasColumnName("state")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.SupersededAt)
            .HasColumnName("superseded_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.SupersessionReason)
            .HasColumnName("supersession_reason")
            .HasMaxLength(500);

        builder.Property(e => e.FailureJson)
            .HasColumnName("failure_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.ResultJson)
            .HasColumnName("result_json")
            .HasColumnType("jsonb");

        builder.HasIndex(e => new { e.DocumentAnalysisId, e.RunNumber })
            .IsUnique()
            .HasDatabaseName("ix_analysis_runs_doc_analysis_run");
    }
}

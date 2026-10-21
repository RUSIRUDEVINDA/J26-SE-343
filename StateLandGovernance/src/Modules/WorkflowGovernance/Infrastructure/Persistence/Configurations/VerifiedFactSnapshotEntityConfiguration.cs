namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class VerifiedFactSnapshotEntityConfiguration : IEntityTypeConfiguration<VerifiedFactSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<VerifiedFactSnapshotEntity> builder)
    {
        builder.ToTable("verified_fact_snapshots", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.DocumentAnalysisId)
            .HasColumnName("document_analysis_id")
            .IsRequired();

        builder.Property(e => e.GovernedDocumentId)
            .HasColumnName("governed_document_id")
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

        builder.Property(e => e.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .IsRequired();

        builder.Property(e => e.AnalysisRunResultId)
            .HasColumnName("analysis_run_result_id")
            .IsRequired();

        builder.Property(e => e.RunNumber)
            .HasColumnName("run_number")
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

        builder.Property(e => e.ResultOutcome)
            .HasColumnName("result_outcome")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SnapshotOutcome)
            .HasColumnName("snapshot_outcome")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EntriesJson)
            .HasColumnName("entries_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.SourceFactCount)
            .HasColumnName("source_fact_count")
            .IsRequired();

        builder.Property(e => e.PublishedFactCount)
            .HasColumnName("published_fact_count")
            .IsRequired();

        builder.Property(e => e.ConfirmedFactCount)
            .HasColumnName("confirmed_fact_count")
            .IsRequired();

        builder.Property(e => e.CorrectedFactCount)
            .HasColumnName("corrected_fact_count")
            .IsRequired();

        builder.Property(e => e.UnsupportedFactCount)
            .HasColumnName("unsupported_fact_count")
            .IsRequired();

        builder.Property(e => e.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.PublishingActorId)
            .HasColumnName("publishing_actor_id")
            .IsRequired();

        builder.Property(e => e.VerifiedCapability)
            .HasColumnName("verified_capability")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.GrantedAuthorityScopeKind)
            .HasColumnName("granted_authority_scope_kind")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.GrantedAuthorityScopeIdentifier)
            .HasColumnName("granted_authority_scope_identifier")
            .HasMaxLength(100);

        builder.Property(e => e.RequiredAuthorityScopeKind)
            .HasColumnName("required_authority_scope_kind")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.RequiredAuthorityScopeIdentifier)
            .HasColumnName("required_authority_scope_identifier")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.AuthorityValidFrom)
            .HasColumnName("authority_valid_from")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.AuthorityVerificationTime)
            .HasColumnName("authority_verification_time")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.AuthorityValidUntil)
            .HasColumnName("authority_valid_until")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.DocumentAnalysisId)
            .HasDatabaseName("ix_verified_fact_snapshots_doc_analysis_id");

        builder.HasIndex(e => e.DocumentVersionId)
            .HasDatabaseName("ix_verified_fact_snapshots_document_version_id");
    }
}

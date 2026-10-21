namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class DocumentVersionEntityConfiguration : IEntityTypeConfiguration<DocumentVersionEntity>
{
    public void Configure(EntityTypeBuilder<DocumentVersionEntity> builder)
    {
        builder.ToTable("document_versions", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.GovernedDocumentId)
            .HasColumnName("governed_document_id")
            .IsRequired();

        builder.Property(e => e.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        builder.Property(e => e.PredecessorVersionId)
            .HasColumnName("predecessor_version_id");

        builder.Property(e => e.ChecksumAlgorithm)
            .HasColumnName("checksum_algorithm")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ChecksumValue)
            .HasColumnName("checksum_value")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.StorageKind)
            .HasColumnName("storage_kind")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StoragePath)
            .HasColumnName("storage_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FileSizeInBytes)
            .HasColumnName("file_size_in_bytes")
            .IsRequired();

        builder.Property(e => e.SubmittedByActorId)
            .HasColumnName("submitted_by_actor_id")
            .IsRequired();

        builder.Property(e => e.SubmittedAt)
            .HasColumnName("submitted_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.State)
            .HasColumnName("state")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => new { e.GovernedDocumentId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_document_versions_governed_doc_ver");
    }
}

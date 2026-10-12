namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class DocumentAnalysisEntityConfiguration : IEntityTypeConfiguration<DocumentAnalysisEntity>
{
    public void Configure(EntityTypeBuilder<DocumentAnalysisEntity> builder)
    {
        builder.ToTable("document_analyses", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

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

        builder.Property(e => e.DocumentVersionNumber)
            .HasColumnName("document_version_number")
            .IsRequired();

        builder.Property(e => e.SourceDocumentRevision)
            .HasColumnName("source_document_revision")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Authoritative unique constraint on DocumentVersionId
        builder.HasIndex(e => e.DocumentVersionId)
            .IsUnique()
            .HasDatabaseName("ix_document_analyses_document_version_id");

        builder.HasIndex(e => e.GovernedDocumentId)
            .HasDatabaseName("ix_document_analyses_governed_document_id");

        builder.HasOne(e => e.GovernedDocument)
            .WithMany(d => d.DocumentAnalyses)
            .HasForeignKey(e => e.GovernedDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Runs)
            .WithOne(r => r.DocumentAnalysis)
            .HasForeignKey(r => r.DocumentAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Verifications)
            .WithOne(v => v.DocumentAnalysis)
            .HasForeignKey(v => v.DocumentAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.VerifiedFactSnapshots)
            .WithOne(s => s.DocumentAnalysis)
            .HasForeignKey(s => s.DocumentAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

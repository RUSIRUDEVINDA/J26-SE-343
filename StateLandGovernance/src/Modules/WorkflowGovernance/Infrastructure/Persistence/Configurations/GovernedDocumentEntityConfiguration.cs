namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class GovernedDocumentEntityConfiguration : IEntityTypeConfiguration<GovernedDocumentEntity>
{
    public void Configure(EntityTypeBuilder<GovernedDocumentEntity> builder)
    {
        builder.ToTable("governed_documents", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.LeaseCaseId)
            .HasColumnName("lease_case_id")
            .IsRequired();

        builder.Property(e => e.LogicalCategory)
            .HasColumnName("logical_category")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ActiveVersionId)
            .HasColumnName("active_version_id")
            .IsRequired();

        builder.Property(e => e.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => e.LeaseCaseId)
            .HasDatabaseName("ix_governed_documents_lease_case_id");

        builder.HasOne(e => e.LeaseCase)
            .WithMany(c => c.GovernedDocuments)
            .HasForeignKey(e => e.LeaseCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Versions)
            .WithOne(v => v.GovernedDocument)
            .HasForeignKey(v => v.GovernedDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

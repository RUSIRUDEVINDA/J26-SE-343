namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class HumanFactVerificationEntityConfiguration : IEntityTypeConfiguration<HumanFactVerificationEntity>
{
    public void Configure(EntityTypeBuilder<HumanFactVerificationEntity> builder)
    {
        builder.ToTable("human_fact_verifications", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.DocumentAnalysisId)
            .HasColumnName("document_analysis_id")
            .IsRequired();

        builder.Property(e => e.FactKey)
            .HasColumnName("fact_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CorrectedValue)
            .HasColumnName("corrected_value");

        builder.Property(e => e.VerifiedByActorId)
            .HasColumnName("verified_by_actor_id")
            .IsRequired();

        builder.Property(e => e.VerifiedAt)
            .HasColumnName("verified_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.Note)
            .HasColumnName("note");

        builder.HasIndex(e => e.DocumentAnalysisId)
            .HasDatabaseName("ix_human_fact_verifications_doc_analysis_id");
    }
}

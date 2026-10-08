using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core entity configuration for early governance screening evaluation snapshots.
/// Configures table naming, column types, and constraints for PostgreSQL and relational providers.
/// </summary>
public class EarlyGovernanceScreeningEvaluationEntityConfiguration : IEntityTypeConfiguration<EarlyGovernanceScreeningEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<EarlyGovernanceScreeningEvaluationEntity> builder)
    {
        builder.ToTable("early_governance_screening_evaluations", GovernanceIntelligenceDbContext.SchemaName);

        builder.HasKey(e => e.AssessmentId);

        builder.Property(e => e.AssessmentId)
            .HasColumnName("assessment_id")
            .ValueGeneratedNever();

        builder.Property(e => e.CaseId)
            .HasColumnName("case_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.InputVersion)
            .HasColumnName("input_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.SnapshotSchemaVersion)
            .HasColumnName("snapshot_schema_version")
            .IsRequired();

        builder.Property(e => e.ResultSnapshotJson)
            .HasColumnName("result_snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="WorkflowAnomalyAssessmentEntity"/> in schema <c>governance_intelligence</c>.
/// </summary>
public sealed class WorkflowAnomalyAssessmentEntityConfiguration
    : IEntityTypeConfiguration<WorkflowAnomalyAssessmentEntity>
{
    public const string TableName = "workflow_anomaly_assessments";

    public void Configure(EntityTypeBuilder<WorkflowAnomalyAssessmentEntity> builder)
    {
        builder.ToTable(TableName, GovernanceIntelligenceDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_case_id_not_blank",
                "length(btrim(case_id)) > 0");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_model_version_not_blank",
                "length(btrim(model_version)) > 0");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_advisory_note_not_blank",
                "length(btrim(advisory_note)) > 0");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_input_sha256_len",
                "char_length(input_sha256) = 64");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_model_sha256_len",
                "model_artifact_sha256 IS NULL OR char_length(model_artifact_sha256) = 64");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_event_count_positive",
                "event_count > 0");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_durations_non_negative",
                "elapsed_days >= 0 AND max_gap_hours >= 0 AND mean_gap_hours >= 0");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_feature_counts_valid",
                "unique_activities > 0 AND repeated_activity_count >= 0 AND resource_handoffs >= 0 AND institution_switches >= 0 AND distinct_resources > 0");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_provenance_kind",
                "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_purpose",
                "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");

            table.HasCheckConstraint(
                "ck_workflow_anomaly_assessments_synthetic_consistency",
                "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) " +
                "OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
        });

        builder.HasKey(x => x.AssessmentId);

        builder.Property(x => x.AssessmentId)
            .HasColumnName("assessment_id")
            .ValueGeneratedNever();

        builder.Property(x => x.WorkflowRunId)
            .HasColumnName("workflow_run_id")
            .IsRequired();

        builder.Property(x => x.CaseId)
            .HasColumnName("case_id")
            .HasMaxLength(WorkflowAnomalyAssessment.MaxCaseIdLength)
            .IsRequired();

        builder.Property(x => x.WorkflowProjectionId)
            .HasColumnName("workflow_projection_id");

        builder.Property(x => x.ModelVersion)
            .HasColumnName("model_version")
            .HasMaxLength(WorkflowAnomalyAssessment.MaxModelVersionLength)
            .IsRequired();

        builder.Property(x => x.ModelArtifactSha256)
            .HasColumnName("model_artifact_sha256")
            .HasMaxLength(64);

        builder.Property(x => x.AnomalyScore)
            .HasColumnName("anomaly_score")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.Threshold)
            .HasColumnName("threshold")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.Flagged)
            .HasColumnName("flagged")
            .IsRequired();

        builder.Property(x => x.EventCount)
            .HasColumnName("event_count")
            .IsRequired();

        builder.Property(x => x.ElapsedDays)
            .HasColumnName("elapsed_days")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.MaxGapHours)
            .HasColumnName("max_gap_hours")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.MeanGapHours)
            .HasColumnName("mean_gap_hours")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(x => x.UniqueActivities)
            .HasColumnName("unique_activities")
            .IsRequired();

        builder.Property(x => x.RepeatedActivityCount)
            .HasColumnName("repeated_activity_count")
            .IsRequired();

        builder.Property(x => x.ResourceHandoffs)
            .HasColumnName("resource_handoffs")
            .IsRequired();

        builder.Property(x => x.InstitutionSwitches)
            .HasColumnName("institution_switches")
            .IsRequired();

        builder.Property(x => x.DistinctResources)
            .HasColumnName("distinct_resources")
            .IsRequired();

        builder.Property(x => x.AdvisoryNote)
            .HasColumnName("advisory_note")
            .HasMaxLength(WorkflowAnomalyAssessment.MaxAdvisoryNoteLength)
            .IsRequired();

        builder.Property(x => x.InputSha256)
            .HasColumnName("input_sha256")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SourceSystem)
            .HasColumnName("source_system")
            .HasMaxLength(GovernanceInputProvenance.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.SourceRecordId)
            .HasColumnName("source_record_id")
            .HasMaxLength(GovernanceInputProvenance.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.SourceRecordVersion)
            .HasColumnName("source_record_version")
            .HasMaxLength(GovernanceInputProvenance.MaxVersionLength)
            .IsRequired();

        builder.Property(x => x.SourceObservedAtUtc)
            .HasColumnName("source_observed_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.IsSynthetic)
            .HasColumnName("is_synthetic")
            .IsRequired();

        builder.Property(x => x.ProvenanceKind)
            .HasColumnName("provenance_kind")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SyntheticSeedId)
            .HasColumnName("synthetic_seed_id")
            .HasMaxLength(GovernanceInputProvenance.MaxIdentifierLength);

        builder.Property(x => x.SyntheticSeedVersion)
            .HasColumnName("synthetic_seed_version")
            .HasMaxLength(GovernanceInputProvenance.MaxVersionLength);

        builder.Property(x => x.GeneratedAtUtc)
            .HasColumnName("generated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AssessedAtUtc)
            .HasColumnName("assessed_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.WorkflowRunId, x.ModelVersion, x.InputSha256 })
            .IsUnique()
            .HasDatabaseName("ux_workflow_anomaly_assessments_run_model_sha256");

        builder.HasIndex(x => x.WorkflowRunId)
            .HasDatabaseName("ix_workflow_anomaly_assessments_workflow_run_id");

        builder.HasIndex(x => x.CaseId)
            .HasDatabaseName("ix_workflow_anomaly_assessments_case_id");
    }
}

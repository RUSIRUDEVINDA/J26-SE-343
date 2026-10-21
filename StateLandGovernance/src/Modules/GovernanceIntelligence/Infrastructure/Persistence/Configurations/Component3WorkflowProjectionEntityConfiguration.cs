using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Component3WorkflowProjectionEntity"/> in schema <c>governance_intelligence</c>.
/// </summary>
public sealed class Component3WorkflowProjectionEntityConfiguration
    : IEntityTypeConfiguration<Component3WorkflowProjectionEntity>
{
    public const string TableName = "component3_workflow_projections";

    public void Configure(EntityTypeBuilder<Component3WorkflowProjectionEntity> builder)
    {
        builder.ToTable(TableName, GovernanceIntelligenceDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint(
                "ck_c3_workflow_projections_case_id_not_blank",
                "length(btrim(case_id)) > 0");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_workflow_type_not_blank",
                "length(btrim(workflow_type)) > 0");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_trace_status",
                "trace_status IN ('Completed', 'Ongoing', 'Stopped', 'Cancelled', 'Suspended')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_trace_completeness",
                "trace_completeness IN ('CompletedTraceEligibleForAnomalyScoring', 'PartialTraceIneligibleForAnomalyScoring', 'InvalidOrIncompleteTrace')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_event_timestamp_semantics",
                "event_timestamp_semantics IN ('ExactUtcTimestamp', 'LocalBusinessDayWithColomboOffset', 'CalendarDateOnlyMidnightColombo')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_event_time_zone",
                "event_time_zone = 'Asia/Colombo'");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_event_count_positive",
                "event_count > 0");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_event_chronology",
                "last_event_timestamp_utc >= first_event_timestamp_utc");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_anomaly_eligibility_consistency",
                "trace_completeness <> 'CompletedTraceEligibleForAnomalyScoring' OR (trace_status <> 'Ongoing' AND final_event_activity = 'DS Final Notification')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_roadmap_start_status",
                "complete_proposal_roadmap_start_status IN ('Confirmed', 'Unconfirmed', 'Unavailable')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_roadmap_start_instant_consistency",
                "(complete_proposal_roadmap_start_status = 'Confirmed' AND complete_proposal_roadmap_start_occurred_at IS NOT NULL) " +
                "OR (complete_proposal_roadmap_start_status = 'Unavailable' AND complete_proposal_roadmap_start_occurred_at IS NULL) " +
                "OR (complete_proposal_roadmap_start_status = 'Unconfirmed')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_handover_status",
                "approval_handover_status IN ('Confirmed', 'Unconfirmed', 'Unavailable')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_handover_instant_consistency",
                "(approval_handover_status = 'Confirmed' AND approval_handover_occurred_at IS NOT NULL AND trace_status <> 'Ongoing') " +
                "OR (approval_handover_status = 'Unavailable' AND approval_handover_occurred_at IS NULL) " +
                "OR (approval_handover_status = 'Unconfirmed')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_input_sha256_len",
                "char_length(input_sha256) = 64");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_provenance_kind",
                "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_purpose",
                "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");

            table.HasCheckConstraint(
                "ck_c3_workflow_projections_synthetic_consistency",
                "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) " +
                "OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
        });

        builder.HasKey(x => x.ProjectionId);

        builder.Property(x => x.ProjectionId)
            .HasColumnName("projection_id")
            .ValueGeneratedNever();

        builder.Property(x => x.WorkflowRunId)
            .HasColumnName("workflow_run_id")
            .IsRequired();

        builder.Property(x => x.CaseId)
            .HasColumnName("case_id")
            .HasMaxLength(Component3WorkflowGovernanceProjection.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.WorkflowType)
            .HasColumnName("workflow_type")
            .HasMaxLength(Component3WorkflowGovernanceProjection.MaxWorkflowTypeLength)
            .IsRequired();

        builder.Property(x => x.TraceStatus)
            .HasColumnName("trace_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TraceCompleteness)
            .HasColumnName("trace_completeness")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CurrentOrTerminalStageCode)
            .HasColumnName("current_or_terminal_stage_code")
            .HasMaxLength(Component3WorkflowGovernanceProjection.MaxStageCodeLength)
            .IsRequired();

        builder.Property(x => x.EventTimestampSemantics)
            .HasColumnName("event_timestamp_semantics")
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(x => x.EventTimeZone)
            .HasColumnName("event_time_zone")
            .HasMaxLength(Component3WorkflowGovernanceProjection.MaxTimeZoneLength)
            .IsRequired();

        builder.Property(x => x.EventCount)
            .HasColumnName("event_count")
            .IsRequired();

        builder.Property(x => x.FirstEventTimestampUtc)
            .HasColumnName("first_event_timestamp_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.LastEventTimestampUtc)
            .HasColumnName("last_event_timestamp_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.FinalEventActivity)
            .HasColumnName("final_event_activity")
            .HasMaxLength(Component3WorkflowProjectionEvent.MaxActivityLength)
            .IsRequired();

        builder.Property(x => x.EventsSnapshotJson)
            .HasColumnName("events_snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CompleteProposalRoadmapStartStatus)
            .HasColumnName("complete_proposal_roadmap_start_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CompleteProposalRoadmapStartOccurredAt)
            .HasColumnName("complete_proposal_roadmap_start_occurred_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CompleteProposalRoadmapStartSource)
            .HasColumnName("complete_proposal_roadmap_start_source")
            .HasMaxLength(Component3WorkflowProjectionEvent.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.CompleteProposalRoadmapStartSourceReference)
            .HasColumnName("complete_proposal_roadmap_start_source_reference")
            .HasMaxLength(Component3WorkflowProjectionEvent.MaxEvidenceRefLength)
            .IsRequired();

        builder.Property(x => x.ApprovalHandoverStatus)
            .HasColumnName("approval_handover_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ApprovalHandoverOccurredAt)
            .HasColumnName("approval_handover_occurred_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ApprovalHandoverSource)
            .HasColumnName("approval_handover_source")
            .HasMaxLength(Component3WorkflowProjectionEvent.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.ApprovalHandoverSourceReference)
            .HasColumnName("approval_handover_source_reference")
            .HasMaxLength(Component3WorkflowProjectionEvent.MaxEvidenceRefLength)
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

        builder.Property(x => x.IngestedAtUtc)
            .HasColumnName("ingested_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.WorkflowRunId, x.SourceRecordVersion, x.InputSha256 })
            .IsUnique()
            .HasDatabaseName("ux_c3_workflow_projections_run_version_sha256");

        builder.HasIndex(x => x.WorkflowRunId)
            .HasDatabaseName("ix_c3_workflow_projections_workflow_run_id");

        builder.HasIndex(x => x.CaseId)
            .HasDatabaseName("ix_c3_workflow_projections_case_id");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="LeaseApprovalTimingAssessmentEntity"/> in schema <c>governance_intelligence</c>.
/// </summary>
public sealed class LeaseApprovalTimingAssessmentEntityConfiguration
    : IEntityTypeConfiguration<LeaseApprovalTimingAssessmentEntity>
{
    public const string TableName = "lease_approval_timing_assessments";

    public void Configure(EntityTypeBuilder<LeaseApprovalTimingAssessmentEntity> builder)
    {
        builder.ToTable(TableName, GovernanceIntelligenceDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_case_id_not_blank",
                "length(btrim(case_id)) > 0");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_policy_version_not_blank",
                "length(btrim(policy_version)) > 0");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_evidence_state_not_blank",
                "length(btrim(evidence_state)) > 0");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_advisory_note_not_blank",
                "length(btrim(advisory_note)) > 0");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_time_zone_colombo",
                "time_zone_id = 'Asia/Colombo'");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_timing_state",
                "timing_state IN ('WithinExpectedTwoCalendarMonths', 'ExceededExpectedTwoCalendarMonths', 'OngoingWithinExpectedTwoCalendarMonths', 'OngoingExceededExpectedTwoCalendarMonths', 'UnavailableEvidence')");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_days_non_negative",
                "(days_elapsed IS NULL OR days_elapsed >= 0) AND (days_beyond_expected IS NULL OR days_beyond_expected >= 0)");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_unavailable_consistency",
                "(timing_state = 'UnavailableEvidence' AND days_beyond_expected IS NULL) " +
                "OR (timing_state <> 'UnavailableEvidence' AND complete_proposal_roadmap_start_date IS NOT NULL AND expected_approval_handover_date IS NOT NULL AND days_beyond_expected IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_input_sha256_len",
                "char_length(input_sha256) = 64");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_provenance_kind",
                "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_purpose",
                "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");

            table.HasCheckConstraint(
                "ck_lease_approval_timing_assessments_synthetic_consistency",
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
            .HasMaxLength(LeaseApprovalTimingAssessment.MaxCaseIdLength)
            .IsRequired();

        builder.Property(x => x.WorkflowProjectionId)
            .HasColumnName("workflow_projection_id");

        builder.Property(x => x.PolicyVersion)
            .HasColumnName("policy_version")
            .HasMaxLength(LeaseApprovalTimingAssessment.MaxPolicyVersionLength)
            .IsRequired();

        builder.Property(x => x.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(LeaseApprovalTimingAssessment.MaxTimeZoneLength)
            .IsRequired();

        builder.Property(x => x.AssessmentDateColombo)
            .HasColumnName("assessment_date_colombo")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.CompleteProposalRoadmapStartDate)
            .HasColumnName("complete_proposal_roadmap_start_date")
            .HasColumnType("date");

        builder.Property(x => x.ExpectedApprovalHandoverDate)
            .HasColumnName("expected_approval_handover_date")
            .HasColumnType("date");

        builder.Property(x => x.ApprovalHandoverDate)
            .HasColumnName("approval_handover_date")
            .HasColumnType("date");

        builder.Property(x => x.DaysElapsed)
            .HasColumnName("days_elapsed");

        builder.Property(x => x.DaysBeyondExpected)
            .HasColumnName("days_beyond_expected");

        builder.Property(x => x.TimingState)
            .HasColumnName("timing_state")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.EvidenceState)
            .HasColumnName("evidence_state")
            .HasMaxLength(LeaseApprovalTimingAssessment.MaxEvidenceStateLength)
            .IsRequired();

        builder.Property(x => x.AdvisoryNote)
            .HasColumnName("advisory_note")
            .HasMaxLength(LeaseApprovalTimingAssessment.MaxAdvisoryNoteLength)
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

        builder.HasIndex(x => new { x.WorkflowRunId, x.PolicyVersion, x.InputSha256 })
            .IsUnique()
            .HasDatabaseName("ux_lease_approval_timing_assessments_run_policy_sha256");

        builder.HasIndex(x => x.WorkflowRunId)
            .HasDatabaseName("ix_lease_approval_timing_assessments_workflow_run_id");

        builder.HasIndex(x => x.CaseId)
            .HasDatabaseName("ix_lease_approval_timing_assessments_case_id");
    }
}

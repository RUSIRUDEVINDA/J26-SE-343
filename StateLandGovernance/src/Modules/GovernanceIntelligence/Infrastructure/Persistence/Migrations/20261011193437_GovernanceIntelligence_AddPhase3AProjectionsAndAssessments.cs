using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddPhase3AProjectionsAndAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "component1_case_projections",
                schema: "governance_intelligence",
                columns: table => new
                {
                    projection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    land_parcel_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    division_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    district_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    province_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    gn_division_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_land_use_category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    extent_ha = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    ownership_evidence_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    protected_zone_overlap_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    overlapping_sensitive_zone_codes_json = table.Column<string>(type: "jsonb", nullable: false),
                    boundary_confirmation_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encumbrance_or_dispute_flag = table.Column<bool>(type: "boolean", nullable: false),
                    parcel_evidence_refs_json = table.Column<string>(type: "jsonb", nullable: false),
                    input_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    provenance_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    synthetic_seed_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    synthetic_seed_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_component1_case_projections", x => x.projection_id);
                    table.CheckConstraint("ck_c1_case_projections_boundary_confirmation_status", "boundary_confirmation_status IN ('BoundaryConfirmed', 'BoundaryPendingSurvey', 'BoundaryDisputed', 'BoundaryEvidenceMissing')");
                    table.CheckConstraint("ck_c1_case_projections_case_id_not_blank", "length(btrim(case_id)) > 0");
                    table.CheckConstraint("ck_c1_case_projections_extent_ha_positive", "extent_ha > 0");
                    table.CheckConstraint("ck_c1_case_projections_input_sha256_len", "char_length(input_sha256) = 64");
                    table.CheckConstraint("ck_c1_case_projections_land_parcel_id_not_blank", "length(btrim(land_parcel_id)) > 0");
                    table.CheckConstraint("ck_c1_case_projections_ownership_evidence_status", "ownership_evidence_status IN ('EvidenceProvided', 'EvidenceMissing', 'EvidenceConflicting', 'EvidenceIncomplete')");
                    table.CheckConstraint("ck_c1_case_projections_protected_zone_overlap_state", "protected_zone_overlap_state IN ('NoProtectedZoneOverlap', 'ProtectedZoneOverlapDetected', 'ProtectedZoneEvidenceIncomplete', 'ProtectedZoneCheckNotPerformed')");
                    table.CheckConstraint("ck_c1_case_projections_provenance_kind", "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");
                    table.CheckConstraint("ck_c1_case_projections_purpose", "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");
                    table.CheckConstraint("ck_c1_case_projections_synthetic_consistency", "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "component3_workflow_projections",
                schema: "governance_intelligence",
                columns: table => new
                {
                    projection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    workflow_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    trace_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    trace_completeness = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_or_terminal_stage_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_timestamp_semantics = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    event_time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_count = table.Column<int>(type: "integer", nullable: false),
                    first_event_timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_event_timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    final_event_activity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    events_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    complete_proposal_roadmap_start_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    complete_proposal_roadmap_start_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    complete_proposal_roadmap_start_source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    complete_proposal_roadmap_start_source_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    approval_handover_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approval_handover_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approval_handover_source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    approval_handover_source_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    input_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    provenance_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    synthetic_seed_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    synthetic_seed_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_component3_workflow_projections", x => x.projection_id);
                    table.CheckConstraint("ck_c3_workflow_projections_anomaly_eligibility_consistency", "trace_completeness <> 'CompletedTraceEligibleForAnomalyScoring' OR (trace_status <> 'Ongoing' AND final_event_activity = 'DS Final Notification')");
                    table.CheckConstraint("ck_c3_workflow_projections_case_id_not_blank", "length(btrim(case_id)) > 0");
                    table.CheckConstraint("ck_c3_workflow_projections_event_chronology", "last_event_timestamp_utc >= first_event_timestamp_utc");
                    table.CheckConstraint("ck_c3_workflow_projections_event_count_positive", "event_count > 0");
                    table.CheckConstraint("ck_c3_workflow_projections_event_time_zone", "event_time_zone = 'Asia/Colombo'");
                    table.CheckConstraint("ck_c3_workflow_projections_event_timestamp_semantics", "event_timestamp_semantics IN ('ExactUtcTimestamp', 'LocalBusinessDayWithColomboOffset', 'CalendarDateOnlyMidnightColombo')");
                    table.CheckConstraint("ck_c3_workflow_projections_handover_instant_consistency", "(approval_handover_status = 'Confirmed' AND approval_handover_occurred_at IS NOT NULL AND trace_status <> 'Ongoing') OR (approval_handover_status = 'Unavailable' AND approval_handover_occurred_at IS NULL) OR (approval_handover_status = 'Unconfirmed')");
                    table.CheckConstraint("ck_c3_workflow_projections_handover_status", "approval_handover_status IN ('Confirmed', 'Unconfirmed', 'Unavailable')");
                    table.CheckConstraint("ck_c3_workflow_projections_input_sha256_len", "char_length(input_sha256) = 64");
                    table.CheckConstraint("ck_c3_workflow_projections_provenance_kind", "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");
                    table.CheckConstraint("ck_c3_workflow_projections_purpose", "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");
                    table.CheckConstraint("ck_c3_workflow_projections_roadmap_start_instant_consistency", "(complete_proposal_roadmap_start_status = 'Confirmed' AND complete_proposal_roadmap_start_occurred_at IS NOT NULL) OR (complete_proposal_roadmap_start_status = 'Unavailable' AND complete_proposal_roadmap_start_occurred_at IS NULL) OR (complete_proposal_roadmap_start_status = 'Unconfirmed')");
                    table.CheckConstraint("ck_c3_workflow_projections_roadmap_start_status", "complete_proposal_roadmap_start_status IN ('Confirmed', 'Unconfirmed', 'Unavailable')");
                    table.CheckConstraint("ck_c3_workflow_projections_synthetic_consistency", "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
                    table.CheckConstraint("ck_c3_workflow_projections_trace_completeness", "trace_completeness IN ('CompletedTraceEligibleForAnomalyScoring', 'PartialTraceIneligibleForAnomalyScoring', 'InvalidOrIncompleteTrace')");
                    table.CheckConstraint("ck_c3_workflow_projections_trace_status", "trace_status IN ('Completed', 'Ongoing', 'Stopped', 'Cancelled', 'Suspended')");
                    table.CheckConstraint("ck_c3_workflow_projections_workflow_type_not_blank", "length(btrim(workflow_type)) > 0");
                });

            migrationBuilder.CreateTable(
                name: "lease_approval_timing_assessments",
                schema: "governance_intelligence",
                columns: table => new
                {
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    workflow_projection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    assessment_date_colombo = table.Column<DateOnly>(type: "date", nullable: false),
                    complete_proposal_roadmap_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_approval_handover_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approval_handover_date = table.Column<DateOnly>(type: "date", nullable: true),
                    days_elapsed = table.Column<int>(type: "integer", nullable: true),
                    days_beyond_expected = table.Column<int>(type: "integer", nullable: true),
                    timing_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evidence_state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    advisory_note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    input_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    provenance_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    synthetic_seed_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    synthetic_seed_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assessed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_approval_timing_assessments", x => x.assessment_id);
                    table.CheckConstraint("ck_lease_approval_timing_assessments_advisory_note_not_blank", "length(btrim(advisory_note)) > 0");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_case_id_not_blank", "length(btrim(case_id)) > 0");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_days_non_negative", "(days_elapsed IS NULL OR days_elapsed >= 0) AND (days_beyond_expected IS NULL OR days_beyond_expected >= 0)");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_evidence_state_not_blank", "length(btrim(evidence_state)) > 0");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_input_sha256_len", "char_length(input_sha256) = 64");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_policy_version_not_blank", "length(btrim(policy_version)) > 0");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_provenance_kind", "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_purpose", "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_synthetic_consistency", "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_time_zone_colombo", "time_zone_id = 'Asia/Colombo'");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_timing_state", "timing_state IN ('WithinExpectedTwoCalendarMonths', 'ExceededExpectedTwoCalendarMonths', 'OngoingWithinExpectedTwoCalendarMonths', 'OngoingExceededExpectedTwoCalendarMonths', 'UnavailableEvidence')");
                    table.CheckConstraint("ck_lease_approval_timing_assessments_unavailable_consistency", "(timing_state = 'UnavailableEvidence' AND days_beyond_expected IS NULL) OR (timing_state <> 'UnavailableEvidence' AND complete_proposal_roadmap_start_date IS NOT NULL AND expected_approval_handover_date IS NOT NULL AND days_beyond_expected IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "workflow_anomaly_assessments",
                schema: "governance_intelligence",
                columns: table => new
                {
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    workflow_projection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_artifact_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    anomaly_score = table.Column<double>(type: "double precision", nullable: false),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    flagged = table.Column<bool>(type: "boolean", nullable: false),
                    event_count = table.Column<int>(type: "integer", nullable: false),
                    elapsed_days = table.Column<double>(type: "double precision", nullable: false),
                    max_gap_hours = table.Column<double>(type: "double precision", nullable: false),
                    mean_gap_hours = table.Column<double>(type: "double precision", nullable: false),
                    unique_activities = table.Column<int>(type: "integer", nullable: false),
                    repeated_activity_count = table.Column<int>(type: "integer", nullable: false),
                    resource_handoffs = table.Column<int>(type: "integer", nullable: false),
                    institution_switches = table.Column<int>(type: "integer", nullable: false),
                    distinct_resources = table.Column<int>(type: "integer", nullable: false),
                    advisory_note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    input_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_system = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_record_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    provenance_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    synthetic_seed_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    synthetic_seed_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assessed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_anomaly_assessments", x => x.assessment_id);
                    table.CheckConstraint("ck_workflow_anomaly_assessments_advisory_note_not_blank", "length(btrim(advisory_note)) > 0");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_case_id_not_blank", "length(btrim(case_id)) > 0");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_durations_non_negative", "elapsed_days >= 0 AND max_gap_hours >= 0 AND mean_gap_hours >= 0");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_event_count_positive", "event_count > 0");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_feature_counts_valid", "unique_activities > 0 AND repeated_activity_count >= 0 AND resource_handoffs >= 0 AND institution_switches >= 0 AND distinct_resources > 0");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_input_sha256_len", "char_length(input_sha256) = 64");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_model_sha256_len", "model_artifact_sha256 IS NULL OR char_length(model_artifact_sha256) = 64");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_model_version_not_blank", "length(btrim(model_version)) > 0");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_provenance_kind", "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_purpose", "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");
                    table.CheckConstraint("ck_workflow_anomaly_assessments_synthetic_consistency", "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_c1_case_projections_case_id",
                schema: "governance_intelligence",
                table: "component1_case_projections",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_c1_case_projections_land_parcel_id",
                schema: "governance_intelligence",
                table: "component1_case_projections",
                column: "land_parcel_id");

            migrationBuilder.CreateIndex(
                name: "ux_c1_case_projections_case_version_sha256",
                schema: "governance_intelligence",
                table: "component1_case_projections",
                columns: new[] { "case_id", "source_record_version", "input_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_c3_workflow_projections_case_id",
                schema: "governance_intelligence",
                table: "component3_workflow_projections",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_c3_workflow_projections_workflow_run_id",
                schema: "governance_intelligence",
                table: "component3_workflow_projections",
                column: "workflow_run_id");

            migrationBuilder.CreateIndex(
                name: "ux_c3_workflow_projections_run_version_sha256",
                schema: "governance_intelligence",
                table: "component3_workflow_projections",
                columns: new[] { "workflow_run_id", "source_record_version", "input_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lease_approval_timing_assessments_case_id",
                schema: "governance_intelligence",
                table: "lease_approval_timing_assessments",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_lease_approval_timing_assessments_workflow_run_id",
                schema: "governance_intelligence",
                table: "lease_approval_timing_assessments",
                column: "workflow_run_id");

            migrationBuilder.CreateIndex(
                name: "ux_lease_approval_timing_assessments_run_policy_sha256",
                schema: "governance_intelligence",
                table: "lease_approval_timing_assessments",
                columns: new[] { "workflow_run_id", "policy_version", "input_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_anomaly_assessments_case_id",
                schema: "governance_intelligence",
                table: "workflow_anomaly_assessments",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_anomaly_assessments_workflow_run_id",
                schema: "governance_intelligence",
                table: "workflow_anomaly_assessments",
                column: "workflow_run_id");

            migrationBuilder.CreateIndex(
                name: "ux_workflow_anomaly_assessments_run_model_sha256",
                schema: "governance_intelligence",
                table: "workflow_anomaly_assessments",
                columns: new[] { "workflow_run_id", "model_version", "input_sha256" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "component1_case_projections",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "component3_workflow_projections",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "lease_approval_timing_assessments",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "workflow_anomaly_assessments",
                schema: "governance_intelligence");
        }
    }
}

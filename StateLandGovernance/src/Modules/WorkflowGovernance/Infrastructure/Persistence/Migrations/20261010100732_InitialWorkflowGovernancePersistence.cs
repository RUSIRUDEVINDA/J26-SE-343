using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflowGovernancePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow_governance");

            migrationBuilder.CreateTable(
                name: "institutional_routing_configs",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rule_set_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    standard_stage_duration_days = table.Column<int>(type: "integer", nullable: false),
                    divisional_secretariat_duration_days = table.Column<int>(type: "integer", nullable: false),
                    commissioner_duration_days = table.Column<int>(type: "integer", nullable: false),
                    standard_capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    divisional_secretary_capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    commissioner_capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_routing_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lease_cases",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_verified_fact_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_intake_json = table.Column<string>(type: "jsonb", nullable: true),
                    latest_screening_json = table.Column<string>(type: "jsonb", nullable: true),
                    active_workflow_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_workflow_json = table.Column<string>(type: "jsonb", nullable: true),
                    handoff_package_json = table.Column<string>(type: "jsonb", nullable: true),
                    approval_conditions_json = table.Column<string>(type: "jsonb", nullable: false),
                    document_submission_requirements_json = table.Column<string>(type: "jsonb", nullable: false),
                    escalations_json = table.Column<string>(type: "jsonb", nullable: false),
                    assessment_history_json = table.Column<string>(type: "jsonb", nullable: false),
                    proposal_content_assessments_json = table.Column<string>(type: "jsonb", nullable: false),
                    workflow_plan_history_json = table.Column<string>(type: "jsonb", nullable: false),
                    proposal_content_sequence_counter = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_completeness_assessments",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_set_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requirement_set_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    assessed_documents_json = table.Column<string>(type: "jsonb", nullable: false),
                    classified_documents_json = table.Column<string>(type: "jsonb", nullable: false),
                    requirements_json = table.Column<string>(type: "jsonb", nullable: false),
                    reviews_json = table.Column<string>(type: "jsonb", nullable: false),
                    missing_requirements_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_completeness_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_completeness_assessments_lease_cases_lease_case_id",
                        column: x => x.lease_case_id,
                        principalSchema: "workflow_governance",
                        principalTable: "lease_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "governed_documents",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    active_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governed_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_governed_documents_lease_cases_lease_case_id",
                        column: x => x.lease_case_id,
                        principalSchema: "workflow_governance",
                        principalTable: "lease_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_executions",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_plan_revision = table.Column<int>(type: "integer", nullable: false),
                    lease_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_reference_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    stages_json = table.Column<string>(type: "jsonb", nullable: false),
                    completed_workflow_json = table.Column<string>(type: "jsonb", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_executions_lease_cases_lease_case_id",
                        column: x => x.lease_case_id,
                        principalSchema: "workflow_governance",
                        principalTable: "lease_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_plans",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    verified_fact_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_completeness_assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_reference_json = table.Column<string>(type: "jsonb", nullable: false),
                    recommendation_reference_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    superseded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    supersession_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    stages_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_plans_lease_cases_lease_case_id",
                        column: x => x.lease_case_id,
                        principalSchema: "workflow_governance",
                        principalTable: "lease_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_analyses",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    governed_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checksum_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    checksum_value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    document_version_number = table.Column<int>(type: "integer", nullable: false),
                    source_document_revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_analyses_governed_documents_governed_document_id",
                        column: x => x.governed_document_id,
                        principalSchema: "workflow_governance",
                        principalTable: "governed_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    governed_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    checksum_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    checksum_value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    storage_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    media_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_in_bytes = table.Column<long>(type: "bigint", nullable: false),
                    submitted_by_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_versions_governed_documents_governed_document_id",
                        column: x => x.governed_document_id,
                        principalSchema: "workflow_governance",
                        principalTable: "governed_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_runs",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_number = table.Column<int>(type: "integer", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checksum_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    checksum_value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_capabilities_json = table.Column<string>(type: "jsonb", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    superseded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    supersession_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_json = table.Column<string>(type: "jsonb", nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_analysis_runs_document_analyses_document_analysis_id",
                        column: x => x.document_analysis_id,
                        principalSchema: "workflow_governance",
                        principalTable: "document_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "human_fact_verifications",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    corrected_value = table.Column<string>(type: "text", nullable: true),
                    verified_by_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_human_fact_verifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_human_fact_verifications_document_analyses_document_analysi~",
                        column: x => x.document_analysis_id,
                        principalSchema: "workflow_governance",
                        principalTable: "document_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verified_fact_snapshots",
                schema: "workflow_governance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    governed_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checksum_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    checksum_value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_number = table.Column<int>(type: "integer", nullable: false),
                    model_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_capabilities_json = table.Column<string>(type: "jsonb", nullable: false),
                    result_outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    snapshot_outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entries_json = table.Column<string>(type: "jsonb", nullable: false),
                    source_fact_count = table.Column<int>(type: "integer", nullable: false),
                    published_fact_count = table.Column<int>(type: "integer", nullable: false),
                    confirmed_fact_count = table.Column<int>(type: "integer", nullable: false),
                    corrected_fact_count = table.Column<int>(type: "integer", nullable: false),
                    unsupported_fact_count = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    publishing_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    granted_authority_scope_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granted_authority_scope_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    required_authority_scope_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    required_authority_scope_identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authority_valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authority_verification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authority_valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_fact_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_verified_fact_snapshots_document_analyses_document_analysis~",
                        column: x => x.document_analysis_id,
                        principalSchema: "workflow_governance",
                        principalTable: "document_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analysis_runs_doc_analysis_run",
                schema: "workflow_governance",
                table: "analysis_runs",
                columns: new[] { "document_analysis_id", "run_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_analyses_document_version_id",
                schema: "workflow_governance",
                table: "document_analyses",
                column: "document_version_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_analyses_governed_document_id",
                schema: "workflow_governance",
                table: "document_analyses",
                column: "governed_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_doc_completeness_assessments_lease_case_id",
                schema: "workflow_governance",
                table: "document_completeness_assessments",
                column: "lease_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_governed_doc_ver",
                schema: "workflow_governance",
                table: "document_versions",
                columns: new[] { "governed_document_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_governed_documents_lease_case_id",
                schema: "workflow_governance",
                table: "governed_documents",
                column: "lease_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_human_fact_verifications_doc_analysis_id",
                schema: "workflow_governance",
                table: "human_fact_verifications",
                column: "document_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_institutional_routing_configs_rule",
                schema: "workflow_governance",
                table: "institutional_routing_configs",
                columns: new[] { "rule_set_name", "rule_set_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lease_cases_application_reference",
                schema: "workflow_governance",
                table: "lease_cases",
                column: "application_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lease_cases_status",
                schema: "workflow_governance",
                table: "lease_cases",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_verified_fact_snapshots_doc_analysis_id",
                schema: "workflow_governance",
                table: "verified_fact_snapshots",
                column: "document_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_verified_fact_snapshots_document_version_id",
                schema: "workflow_governance",
                table: "verified_fact_snapshots",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_lease_case_id",
                schema: "workflow_governance",
                table: "workflow_executions",
                column: "lease_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_workflow_plan_id",
                schema: "workflow_governance",
                table: "workflow_executions",
                column: "workflow_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_plans_lease_case_id",
                schema: "workflow_governance",
                table: "workflow_plans",
                column: "lease_case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_runs",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "document_completeness_assessments",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "human_fact_verifications",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "institutional_routing_configs",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "verified_fact_snapshots",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "workflow_executions",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "workflow_plans",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "document_analyses",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "governed_documents",
                schema: "workflow_governance");

            migrationBuilder.DropTable(
                name: "lease_cases",
                schema: "workflow_governance");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficerInvestigationsAndAssessmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "officer_investigations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FindingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SupersedesId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_officer_investigations", x => x.Id);
                    table.UniqueConstraint("AK_officer_investigations_Id_CreatorActorId", x => new { x.Id, x.CreatorActorId });
                    table.CheckConstraint("ck_officer_findings", "jsonb_typeof(\"FindingsJson\")='array' AND jsonb_array_length(\"FindingsJson\")=7");
                    table.CheckConstraint("ck_officer_input_hash", "length(\"InputSha256\")=64");
                    table.ForeignKey(
                        name: "FK_officer_investigations_officer_investigations_SupersedesId_~",
                        columns: x => new { x.SupersedesId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_investigations",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "officer_assessment_requests",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvestigationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scenario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EarlyAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnomalyAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_officer_assessment_requests", x => x.Id);
                    table.UniqueConstraint("AK_officer_assessment_requests_Id_CreatorActorId", x => new { x.Id, x.CreatorActorId });
                    table.CheckConstraint("ck_officer_kind", "\"Kind\" IN ('early','anomaly')");
                    table.CheckConstraint("ck_officer_request_hash", "length(\"InputSha256\")=64");
                    table.CheckConstraint("ck_officer_request_source", "(\"Kind\"='early' AND \"InvestigationId\" IS NOT NULL AND \"Scenario\" IS NULL AND \"WorkflowRunId\" IS NULL AND \"ProjectionId\" IS NULL) OR (\"Kind\"='anomaly' AND \"InvestigationId\" IS NULL AND \"Scenario\" IS NOT NULL AND \"WorkflowRunId\" IS NOT NULL AND \"ProjectionId\" IS NOT NULL)");
                    table.CheckConstraint("ck_officer_result", "(\"State\"='Saved' AND \"FinishedAtUtc\" IS NOT NULL AND ((\"Kind\"='early' AND \"EarlyAssessmentId\" IS NOT NULL AND \"AnomalyAssessmentId\" IS NULL) OR (\"Kind\"='anomaly' AND \"AnomalyAssessmentId\" IS NOT NULL AND \"EarlyAssessmentId\" IS NULL))) OR (\"State\"<>'Saved' AND \"EarlyAssessmentId\" IS NULL AND \"AnomalyAssessmentId\" IS NULL)");
                    table.CheckConstraint("ck_officer_revision", "\"Revision\">0");
                    table.CheckConstraint("ck_officer_state", "\"State\" IN ('Pending','Saved','Failed','Unknown')");
                    table.ForeignKey(
                        name: "FK_officer_assessment_requests_component3_workflow_projections~",
                        column: x => x.ProjectionId,
                        principalSchema: "governance_intelligence",
                        principalTable: "component3_workflow_projections",
                        principalColumn: "projection_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_officer_assessment_requests_early_governance_screening_eval~",
                        column: x => x.EarlyAssessmentId,
                        principalSchema: "governance_intelligence",
                        principalTable: "early_governance_screening_evaluations",
                        principalColumn: "assessment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_officer_assessment_requests_officer_investigations_Investig~",
                        columns: x => new { x.InvestigationId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_investigations",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_officer_assessment_requests_workflow_anomaly_assessments_An~",
                        column: x => x.AnomalyAssessmentId,
                        principalSchema: "governance_intelligence",
                        principalTable: "workflow_anomaly_assessments",
                        principalColumn: "assessment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "officer_action_events",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestigationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EventSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_officer_action_events", x => x.Id);
                    table.CheckConstraint("ck_officer_event_hash", "length(\"EventSha256\")=64");
                    table.CheckConstraint("ck_officer_event_parent", "(\"InvestigationId\" IS NULL) <> (\"RequestId\" IS NULL)");
                    table.CheckConstraint("ck_officer_operation", "\"Operation\" IN ('InvestigationSaved','RequestSubmitted','InferenceStarted','AssessmentSaved','AssessmentFailed','OutcomeUnknown')");
                    table.ForeignKey(
                        name: "FK_officer_action_events_officer_assessment_requests_RequestId~",
                        columns: x => new { x.RequestId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_assessment_requests",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_officer_action_events_officer_investigations_InvestigationI~",
                        columns: x => new { x.InvestigationId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_investigations",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_officer_action_events_request",
                schema: "governance_intelligence",
                table: "officer_action_events",
                columns: new[] { "RequestId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ux_officer_action_request_operation",
                schema: "governance_intelligence",
                table: "officer_action_events",
                columns: new[] { "RequestId", "Operation" },
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_officer_assessment_history",
                schema: "governance_intelligence",
                table: "officer_assessment_requests",
                columns: new[] { "CreatorActorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_officer_assessment_requests_CreatorActorId_SubmissionKey",
                schema: "governance_intelligence",
                table: "officer_assessment_requests",
                columns: new[] { "CreatorActorId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_officer_assessment_requests_WorkflowRunId",
                schema: "governance_intelligence",
                table: "officer_assessment_requests",
                column: "WorkflowRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_officer_assessment_active_scope",
                schema: "governance_intelligence",
                table: "officer_assessment_requests",
                columns: new[] { "CreatorActorId", "Kind", "ScopeKey", "SourceVersion" },
                unique: true,
                filter: "\"State\" IN ('Pending','Unknown')");

            migrationBuilder.CreateIndex(
                name: "IX_officer_investigations_CreatorActorId_SubmissionKey",
                schema: "governance_intelligence",
                table: "officer_investigations",
                columns: new[] { "CreatorActorId", "SubmissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Officer investigation and assessment history must be preserved.");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintHandlingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "complaint_handling_events",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplaintId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreviousEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HandlingMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResponsibleActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvidenceReferencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublicSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorizationGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizationSourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EventCanonicalJson = table.Column<string>(type: "text", nullable: false),
                    EventSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaint_handling_events", x => x.Id);
                    table.UniqueConstraint("AK_complaint_handling_event_parent", x => new { x.Id, x.ComplaintId });
                    table.UniqueConstraint("UQ_complaint_handling_revision", x => new { x.ComplaintId, x.Revision });
                    table.UniqueConstraint("UQ_complaint_handling_submission", x => new { x.ActorId, x.SubmissionKey });
                    table.CheckConstraint("CK_complaint_handling_authority", "\n  (\"AuthorizationGrantId\" IS NULL AND \"AuthorizationSourceReference\" IS NULL AND \"HandlingMode\"='Ds' AND\n   \"Operation\" IN ('StartInvestigation','RecordNote','RequestInformation','RecordFindings','DsReviewRecorded')) OR\n  (\"AuthorizationGrantId\" IS NOT NULL AND \"AuthorizationSourceReference\" IS NOT NULL AND length(btrim(\"AuthorizationSourceReference\"))>0)");
                    table.CheckConstraint("CK_complaint_handling_canonical", "length(btrim(\"EventCanonicalJson\"))>0");
                    table.CheckConstraint("CK_complaint_handling_details", "jsonb_typeof(\"DetailsJson\")='object' AND octet_length(\"DetailsJson\"::text)<=32768");
                    table.CheckConstraint("CK_complaint_handling_evidence", "\n  CASE WHEN jsonb_typeof(\"EvidenceReferencesJson\")='array' THEN jsonb_array_length(\"EvidenceReferencesJson\")<=20 ELSE false END");
                    table.CheckConstraint("CK_complaint_handling_hashes", "\n  \"InputSha256\" ~ '^[0-9a-f]{64}$' AND \"EventSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_complaint_handling_operation", "\"Operation\" IN\n  ('TakeDirect','AssignDs','ReassignDs','StartInvestigation','RecordNote','RequestInformation','RecordFindings','Resolve','Close','Reopen','DsReviewRecorded')");
                    table.CheckConstraint("CK_complaint_handling_reason", "length(btrim(\"Reason\"))>0");
                    table.CheckConstraint("CK_complaint_handling_responsibility", "\n  (\"HandlingMode\"='Commissioner' AND \"ResponsibleActorId\" IS NOT NULL AND \"AssignmentEventId\" IS NULL AND\n   \"ToState\" IN ('UnderCommissionerReview','InformationRequested','UnderInvestigation','Reopened')) OR\n  (\"HandlingMode\"='Ds' AND \"ResponsibleActorId\" IS NOT NULL AND \"AssignmentEventId\" IS NOT NULL AND\n   \"ToState\" IN ('AssignedToDs','InformationRequested','UnderInvestigation')) OR\n  (\"HandlingMode\"='None' AND \"ResponsibleActorId\" IS NULL AND \"AssignmentEventId\" IS NULL AND \"ToState\" IN ('Resolved','Closed'))");
                    table.CheckConstraint("CK_complaint_handling_revision", "\n  (\"Revision\"=1 AND \"PreviousEventId\" IS NULL) OR (\"Revision\">1 AND \"PreviousEventId\" IS NOT NULL)");
                    table.CheckConstraint("CK_complaint_handling_states", "\n  \"FromState\" IN ('Unassigned','UnderCommissionerReview','AssignedToDs','InformationRequested','UnderInvestigation','Resolved','Closed','Reopened') AND\n  \"ToState\" IN ('Unassigned','UnderCommissionerReview','AssignedToDs','InformationRequested','UnderInvestigation','Resolved','Closed','Reopened')");
                    table.ForeignKey(
                        name: "FK_complaint_handling_assignment",
                        columns: x => new { x.AssignmentEventId, x.ComplaintId },
                        principalSchema: "governance_intelligence",
                        principalTable: "complaint_assignment_events",
                        principalColumns: new[] { "Id", "ComplaintId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaint_handling_complaint",
                        column: x => x.ComplaintId,
                        principalSchema: "governance_intelligence",
                        principalTable: "registered_complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaint_handling_previous",
                        columns: x => new { x.PreviousEventId, x.ComplaintId },
                        principalSchema: "governance_intelligence",
                        principalTable: "complaint_handling_events",
                        principalColumns: new[] { "Id", "ComplaintId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_complaint_handling_responsible",
                schema: "governance_intelligence",
                table: "complaint_handling_events",
                columns: new[] { "ResponsibleActorId", "ComplaintId", "Revision" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_complaint_handling_status",
                schema: "governance_intelligence",
                table: "complaint_handling_events",
                columns: new[] { "ToState", "ComplaintId", "Revision" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_complaint_handling_one_successor",
                schema: "governance_intelligence",
                table: "complaint_handling_events",
                column: "PreviousEventId",
                unique: true,
                filter: "\"PreviousEventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve immutable complaint handling and decisions; disable access and retain history during recovery.");
        }
    }
}

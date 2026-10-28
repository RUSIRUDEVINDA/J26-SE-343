using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralComplaintAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "complaint_assignment_events",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplaintId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TargetActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetOfficeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedByActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DirectorySnapshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorizationGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorizationSourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventCanonicalJson = table.Column<string>(type: "text", nullable: false),
                    EventSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaint_assignment_events", x => x.Id);
                    table.UniqueConstraint("AK_complaint_assignment_events_Id_ComplaintId", x => new { x.Id, x.ComplaintId });
                    table.UniqueConstraint("UQ_complaint_assignment_revision", x => new { x.ComplaintId, x.Revision });
                    table.UniqueConstraint("UQ_complaint_assignment_submission", x => new { x.AssignedByActorId, x.SubmissionKey });
                    table.CheckConstraint("CK_complaint_assignment_authority", "length(btrim(\"AuthorizationSourceReference\"))>0");
                    table.CheckConstraint("CK_complaint_assignment_canonical", "length(btrim(\"EventCanonicalJson\"))>0");
                    table.CheckConstraint("CK_complaint_assignment_chain", "(\"Revision\"=1 AND \"PreviousAssignmentId\" IS NULL) OR (\"Revision\">1 AND \"PreviousAssignmentId\" IS NOT NULL)");
                    table.CheckConstraint("CK_complaint_assignment_hashes", "\"InputSha256\" ~ '^[0-9a-f]{64}$' AND \"DirectorySnapshotSha256\" ~ '^[0-9a-f]{64}$' AND \"EventSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_complaint_assignment_office", "length(btrim(\"TargetOfficeCode\"))>0");
                    table.CheckConstraint("CK_complaint_assignment_reason", "length(btrim(\"Reason\"))>0");
                    table.CheckConstraint("CK_complaint_assignment_revision", "\"Revision\">0");
                    table.ForeignKey(
                        name: "FK_complaint_assignment_complaint",
                        column: x => x.ComplaintId,
                        principalSchema: "governance_intelligence",
                        principalTable: "registered_complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaint_assignment_previous",
                        columns: x => new { x.PreviousAssignmentId, x.ComplaintId },
                        principalSchema: "governance_intelligence",
                        principalTable: "complaint_assignment_events",
                        principalColumns: new[] { "Id", "ComplaintId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_complaint_assignment_target",
                schema: "governance_intelligence",
                table: "complaint_assignment_events",
                columns: new[] { "TargetActorId", "ComplaintId", "Revision" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_complaint_assignment_one_successor",
                schema: "governance_intelligence",
                table: "complaint_assignment_events",
                column: "PreviousAssignmentId",
                unique: true,
                filter: "\"PreviousAssignmentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Preserve complaint assignment history; disable access and revoke only new grants for rollback.");
        }
    }
}

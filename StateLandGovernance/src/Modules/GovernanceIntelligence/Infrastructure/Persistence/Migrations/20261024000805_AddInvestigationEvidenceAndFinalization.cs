using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestigationEvidenceAndFinalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_officer_operation",
                schema: "governance_intelligence",
                table: "officer_action_events");

            migrationBuilder.CreateTable(
                name: "officer_investigation_attachments",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestigationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Concern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp(6) with time zone", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_investigation_attachments", x => x.Id);
                    table.UniqueConstraint("uq_investigation_attachment_submission", x => new { x.CreatorActorId, x.SubmissionKey });
                    table.CheckConstraint("ck_investigation_attachment_bytes", "octet_length(\"Content\") BETWEEN 1 AND 5242880");
                    table.CheckConstraint("ck_investigation_attachment_concern", "\"Concern\" IN ('LegalDispute','UnauthorizedOccupation','UnauthorizedConstruction','FamilyOrInheritanceClaim','MultipleClaimants','UnresolvedObjection','PreviousIllegalLandActivity')");
                    table.CheckConstraint("ck_investigation_attachment_content_hash", "encode(sha256(\"Content\"),'hex')=\"ContentSha256\"");
                    table.CheckConstraint("ck_investigation_attachment_filename", "length(btrim(\"FileName\")) BETWEEN 1 AND 150 AND position('/' in \"FileName\")=0 AND position(chr(92) in \"FileName\")=0 AND position(':' in \"FileName\")=0 AND \"FileName\" !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_investigation_attachment_hashes", "\"ContentSha256\" ~ '^[0-9a-f]{64}$' AND \"InputSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_investigation_attachment_identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CreatorActorId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"SubmissionKey\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_investigation_attachment_type", "\"ContentType\" IN ('application/pdf','image/png','image/jpeg','text/plain')");
                    table.ForeignKey(
                        name: "fk_investigation_attachment_owner",
                        columns: x => new { x.InvestigationId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_investigations",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_officer_investigation_finalization",
                schema: "governance_intelligence",
                table: "officer_action_events",
                column: "InvestigationId",
                unique: true,
                filter: "\"Operation\" = 'InvestigationFinalized'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_officer_operation",
                schema: "governance_intelligence",
                table: "officer_action_events",
                sql: "\"Operation\" IN ('InvestigationSaved','RequestSubmitted','InferenceStarted','AssessmentSaved','AssessmentFailed','OutcomeUnknown') OR (\"Operation\"='InvestigationFinalized' AND \"InvestigationId\" IS NOT NULL AND \"RequestId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_investigation_attachment_history",
                schema: "governance_intelligence",
                table: "officer_investigation_attachments",
                columns: new[] { "InvestigationId", "CreatedAtUtc", "Id" });


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Append-only evidence and finalization history must be retained. Use reviewed fail-closed recovery.");
        }
    }
}

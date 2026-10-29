using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreeningEvidenceVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "screening_evidence_attachments",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Concern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screening_evidence_attachments", x => x.Id);
                    table.CheckConstraint("ck_screening_attachment_bytes", "octet_length(\"Content\") BETWEEN 1 AND 5242880");
                    table.CheckConstraint("ck_screening_attachment_hashes", "length(\"ContentSha256\")=64 AND length(\"InputSha256\")=64");
                    table.CheckConstraint("ck_screening_attachment_type", "\"ContentType\" IN ('application/pdf','image/png','image/jpeg','text/plain')");
                    table.ForeignKey(
                        name: "FK_screening_evidence_attachments_officer_assessment_requests_~",
                        columns: x => new { x.RequestId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_assessment_requests",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "screening_evidence_verifications",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Concern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreviousEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AttachmentIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReferencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalJson = table.Column<string>(type: "text", nullable: false),
                    EventSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screening_evidence_verifications", x => x.Id);
                    table.UniqueConstraint("AK_screening_evidence_verifications_Id_RequestId_Concern_Creat~", x => new { x.Id, x.RequestId, x.Concern, x.CreatorActorId });
                    table.CheckConstraint("ck_screening_verification_hash", "length(\"InputSha256\")=64 AND length(\"EventSha256\")=64");
                    table.CheckConstraint("ck_screening_verification_refs", "jsonb_typeof(\"AttachmentIdsJson\")='array' AND jsonb_typeof(\"ReferencesJson\")='array' AND jsonb_array_length(\"AttachmentIdsJson\")<=10 AND jsonb_array_length(\"ReferencesJson\")<=10");
                    table.CheckConstraint("ck_screening_verification_remarks", "length(\"Remarks\") BETWEEN 20 AND 2000");
                    table.CheckConstraint("ck_screening_verification_revision", "\"Revision\">0 AND ((\"Revision\"=1 AND \"PreviousEventId\" IS NULL AND \"PreviousStatus\"='Unverified') OR (\"Revision\">1 AND \"PreviousEventId\" IS NOT NULL))");
                    table.CheckConstraint("ck_screening_verification_status", "\"Status\" IN ('Verified','Rejected','MoreInformationRequired') AND \"PreviousStatus\" IN ('Unverified','Verified','Rejected','MoreInformationRequired')");
                    table.ForeignKey(
                        name: "FK_screening_evidence_verifications_officer_assessment_request~",
                        columns: x => new { x.RequestId, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "officer_assessment_requests",
                        principalColumns: new[] { "Id", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_screening_evidence_verifications_screening_evidence_verific~",
                        columns: x => new { x.PreviousEventId, x.RequestId, x.Concern, x.CreatorActorId },
                        principalSchema: "governance_intelligence",
                        principalTable: "screening_evidence_verifications",
                        principalColumns: new[] { "Id", "RequestId", "Concern", "CreatorActorId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_attachments_CreatorActorId_SubmissionKey",
                schema: "governance_intelligence",
                table: "screening_evidence_attachments",
                columns: new[] { "CreatorActorId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_attachments_RequestId_CreatedAtUtc",
                schema: "governance_intelligence",
                table: "screening_evidence_attachments",
                columns: new[] { "RequestId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_attachments_RequestId_CreatorActorId",
                schema: "governance_intelligence",
                table: "screening_evidence_attachments",
                columns: new[] { "RequestId", "CreatorActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_verifications_CreatorActorId_SubmissionK~",
                schema: "governance_intelligence",
                table: "screening_evidence_verifications",
                columns: new[] { "CreatorActorId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_verifications_PreviousEventId",
                schema: "governance_intelligence",
                table: "screening_evidence_verifications",
                column: "PreviousEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_verifications_PreviousEventId_RequestId_~",
                schema: "governance_intelligence",
                table: "screening_evidence_verifications",
                columns: new[] { "PreviousEventId", "RequestId", "Concern", "CreatorActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_verifications_RequestId_Concern_Revision",
                schema: "governance_intelligence",
                table: "screening_evidence_verifications",
                columns: new[] { "RequestId", "Concern", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_screening_evidence_verifications_RequestId_CreatorActorId",
                schema: "governance_intelligence",
                table: "screening_evidence_verifications",
                columns: new[] { "RequestId", "CreatorActorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Manual evidence and verification history must be preserved; destructive Down is prohibited.");
        }
    }
}

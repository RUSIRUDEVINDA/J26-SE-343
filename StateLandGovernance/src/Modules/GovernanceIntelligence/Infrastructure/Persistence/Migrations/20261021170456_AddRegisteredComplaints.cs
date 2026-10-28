using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegisteredComplaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registered_complaints",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registered_complaints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "registered_complaint_classifications",
                schema: "governance_intelligence",
                columns: table => new
                {
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplaintId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registered_complaint_classifications", x => x.AssessmentId);
                    table.ForeignKey(
                        name: "FK_registered_complaint_classifications_complaint_classificati~",
                        column: x => x.AssessmentId,
                        principalSchema: "governance_intelligence",
                        principalTable: "complaint_classification_assessments",
                        principalColumn: "assessment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registered_complaint_classifications_registered_complaints_~",
                        column: x => x.ComplaintId,
                        principalSchema: "governance_intelligence",
                        principalTable: "registered_complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registered_complaint_classifications_ComplaintId",
                schema: "governance_intelligence",
                table: "registered_complaint_classifications",
                column: "ComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_registered_complaints_ApplicantId_SubmissionKey",
                schema: "governance_intelligence",
                table: "registered_complaints",
                columns: new[] { "ApplicantId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registered_complaints_CaseId_SubmittedAtUtc",
                schema: "governance_intelligence",
                table: "registered_complaints",
                columns: new[] { "CaseId", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Forward-only complaint history. Disable the feature; preserve complaints, assessments and reviews.");
        }
    }
}

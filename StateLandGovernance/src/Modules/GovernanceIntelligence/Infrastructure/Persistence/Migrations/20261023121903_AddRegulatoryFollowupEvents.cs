using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryFollowupEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulatory_followup_events",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewOutcome = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    MissingInformation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreviousEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanonicalJson = table.Column<string>(type: "text", nullable: false),
                    EventSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_followup_events", x => x.Id);
                    table.UniqueConstraint("AK_regulatory_followup_events_Id_EvaluationId_RuleCode", x => new { x.Id, x.EvaluationId, x.RuleCode });
                    table.CheckConstraint("ck_regulatory_followup_hash", "\"InputSha256\" ~ '^[0-9a-f]{64}$' AND \"EventSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_regulatory_followup_operation", "(\"Operation\"='ClarificationRequested' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NOT NULL AND length(\"MissingInformation\") BETWEEN 20 AND 2000) OR (\"Operation\"='HumanReviewRecorded' AND \"MissingInformation\" IS NULL AND \"ReviewOutcome\" IS NOT NULL AND \"ReviewOutcome\" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview'))");
                    table.CheckConstraint("ck_regulatory_followup_refs", "jsonb_typeof(\"ReferencesJson\")='array' AND jsonb_array_length(\"ReferencesJson\")<=10 AND NOT jsonb_path_exists(\"ReferencesJson\", '$[*] ? (@.type() != \"string\")')");
                    table.CheckConstraint("ck_regulatory_followup_remarks", "length(\"Remarks\") BETWEEN 20 AND 2000");
                    table.CheckConstraint("ck_regulatory_followup_revision", "(\"Revision\"=1 AND \"PreviousEventId\" IS NULL) OR (\"Revision\">1 AND \"PreviousEventId\" IS NOT NULL)");
                    table.CheckConstraint("ck_regulatory_followup_rule", "length(\"RuleCode\")>0 AND length(\"RuleVersion\")>0");
                    table.CheckConstraint("ck_regulatory_followup_status", "\"Status\"='Recorded'");
                    table.ForeignKey(
                        name: "FK_regulatory_followup_events_compliance_evaluations_Evaluatio~",
                        column: x => x.EvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regulatory_followup_events_regulatory_followup_events_Previ~",
                        columns: x => new { x.PreviousEventId, x.EvaluationId, x.RuleCode },
                        principalSchema: "governance_intelligence",
                        principalTable: "regulatory_followup_events",
                        principalColumns: new[] { "Id", "EvaluationId", "RuleCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_followup_events_ActorId_SubmissionKey",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                columns: new[] { "ActorId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_followup_events_EvaluationId_CreatedAtUtc",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                columns: new[] { "EvaluationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_followup_events_EvaluationId_RuleCode_Revision",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                columns: new[] { "EvaluationId", "RuleCode", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_followup_events_PreviousEventId",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                column: "PreviousEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_followup_events_PreviousEventId_EvaluationId_Rul~",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                columns: new[] { "PreviousEventId", "EvaluationId", "RuleCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Regulatory follow-up history must be retained. Disable endpoints and revoke only added grants; no destructive Down.");
        }
    }
}

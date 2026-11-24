using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowDirectOfficerDisposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regulatory_followup_operation",
                schema: "governance_intelligence",
                table: "regulatory_followup_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_regulatory_followup_remarks",
                schema: "governance_intelligence",
                table: "regulatory_followup_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulatory_followup_operation",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                sql: "(\"Operation\"='ClarificationRequested' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NOT NULL AND length(\"MissingInformation\") BETWEEN 20 AND 2000) OR (\"Operation\"='HumanReviewRecorded' AND \"MissingInformation\" IS NULL AND \"ReviewOutcome\" IS NOT NULL AND \"ReviewOutcome\" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview')) OR (\"Operation\"='NoFollowupRequired' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL) OR (\"Operation\"='FindingVerifiedClosed' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL) ");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulatory_followup_remarks",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                sql: "length(\"Remarks\") <= 2000 AND (\"Operation\"='FindingVerifiedClosed' OR length(\"Remarks\")>=20)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Preserve immutable officer dispositions; use reviewed forward-only recovery.");
        }
    }
}
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowRegulatoryNoFollowupRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regulatory_followup_operation",
                schema: "governance_intelligence",
                table: "regulatory_followup_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulatory_followup_operation",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                sql: "(\"Operation\"='ClarificationRequested' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NOT NULL AND length(\"MissingInformation\") BETWEEN 20 AND 2000) OR (\"Operation\"='HumanReviewRecorded' AND \"MissingInformation\" IS NULL AND \"ReviewOutcome\" IS NOT NULL AND \"ReviewOutcome\" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview')) OR (\"Operation\"='NoFollowupRequired' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Retain append-only follow-up history and the widened operation CHECK. Use reviewed fail-closed recovery.");
        }
    }
}

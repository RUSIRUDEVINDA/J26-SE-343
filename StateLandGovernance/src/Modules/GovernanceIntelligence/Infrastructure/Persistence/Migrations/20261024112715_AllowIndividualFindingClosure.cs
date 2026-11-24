using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowIndividualFindingClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regulatory_followup_operation",
                schema: "governance_intelligence",
                table: "regulatory_followup_events");

            migrationBuilder.CreateIndex(
                name: "ux_regulatory_finding_closure",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                columns: new[] { "EvaluationId", "RuleCode" },
                unique: true,
                filter: "\"Operation\"='FindingVerifiedClosed'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulatory_followup_operation",
                schema: "governance_intelligence",
                table: "regulatory_followup_events",
                sql: "(\"Operation\"='ClarificationRequested' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NOT NULL AND length(\"MissingInformation\") BETWEEN 20 AND 2000) OR (\"Operation\"='HumanReviewRecorded' AND \"MissingInformation\" IS NULL AND \"ReviewOutcome\" IS NOT NULL AND \"ReviewOutcome\" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview')) OR (\"Operation\"='NoFollowupRequired' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL) OR (\"Operation\"='FindingVerifiedClosed' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL AND length(\"Remarks\")>=40 AND jsonb_array_length(\"ReferencesJson\")>=1) ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Preserve immutable finding closures. Use reviewed forward-only recovery.");
        }
    }
}

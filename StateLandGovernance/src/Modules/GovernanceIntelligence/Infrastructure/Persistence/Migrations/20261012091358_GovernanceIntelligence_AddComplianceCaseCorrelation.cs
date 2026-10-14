using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddComplianceCaseCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseId",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_comp_eval_case_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                column: "CaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_comp_eval_case_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "CaseId",
                schema: "governance_intelligence",
                table: "compliance_evaluations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_AddRichComplianceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeterministicEvaluationId",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FindingsJson",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalId",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleSetVersion",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "regulatory_sources",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Authority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DocumentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GazetteNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PublishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Checksum = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compliance_rules",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RuleType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CalculationKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_rules_regulatory_sources_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "governance_intelligence",
                        principalTable: "regulatory_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rule_parameters",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParameterName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ParameterValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_parameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_parameters_compliance_rules_RuleId",
                        column: x => x.RuleId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_comp_eval_det_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                column: "DeterministicEvaluationId");

            migrationBuilder.CreateIndex(
                name: "idx_comp_eval_proposal_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "idx_uniq_rule_code_version",
                schema: "governance_intelligence",
                table: "compliance_rules",
                columns: new[] { "RuleCode", "RuleVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_rules_SourceId",
                schema: "governance_intelligence",
                table: "compliance_rules",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "idx_rule_param_rule_id",
                schema: "governance_intelligence",
                table: "rule_parameters",
                column: "RuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_parameters",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "compliance_rules",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "regulatory_sources",
                schema: "governance_intelligence");

            migrationBuilder.DropIndex(
                name: "idx_comp_eval_det_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations");

            migrationBuilder.DropIndex(
                name: "idx_comp_eval_proposal_id",
                schema: "governance_intelligence",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "DeterministicEvaluationId",
                schema: "governance_intelligence",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "FindingsJson",
                schema: "governance_intelligence",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                schema: "governance_intelligence",
                table: "compliance_evaluations");

            migrationBuilder.DropColumn(
                name: "RuleSetVersion",
                schema: "governance_intelligence",
                table: "compliance_evaluations");
        }
    }
}

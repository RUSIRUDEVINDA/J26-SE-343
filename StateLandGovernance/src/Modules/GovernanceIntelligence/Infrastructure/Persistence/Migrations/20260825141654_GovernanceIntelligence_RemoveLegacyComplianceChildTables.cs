using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_RemoveLegacyComplianceChildTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_conditions",
                schema: "governance_intelligence");

            migrationBuilder.DropTable(
                name: "compliance_violations",
                schema: "governance_intelligence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_conditions",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    RequiredByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_conditions_compliance_evaluations_ComplianceEval~",
                        column: x => x.ComplianceEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compliance_violations",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_violations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_violations_compliance_evaluations_ComplianceEval~",
                        column: x => x.ComplianceEvaluationId,
                        principalSchema: "governance_intelligence",
                        principalTable: "compliance_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_conditions_ComplianceEvaluationId",
                schema: "governance_intelligence",
                table: "compliance_conditions",
                column: "ComplianceEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_violations_ComplianceEvaluationId",
                schema: "governance_intelligence",
                table: "compliance_violations",
                column: "ComplianceEvaluationId");
        }
    }
}

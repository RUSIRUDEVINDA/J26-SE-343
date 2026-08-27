using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIntelligence_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "governance_intelligence");

            migrationBuilder.CreateTable(
                name: "governance_audit_records",
                schema: "governance_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EngineType = table.Column<int>(type: "integer", nullable: false),
                    ActionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_audit_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_gov_audit_action_name",
                schema: "governance_intelligence",
                table: "governance_audit_records",
                column: "ActionName");

            migrationBuilder.CreateIndex(
                name: "idx_gov_audit_engine_type",
                schema: "governance_intelligence",
                table: "governance_audit_records",
                column: "EngineType");

            migrationBuilder.CreateIndex(
                name: "idx_gov_audit_timestamp",
                schema: "governance_intelligence",
                table: "governance_audit_records",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "governance_audit_records",
                schema: "governance_intelligence");
        }
    }
}

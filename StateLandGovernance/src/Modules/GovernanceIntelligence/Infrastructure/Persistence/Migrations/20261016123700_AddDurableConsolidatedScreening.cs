using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableConsolidatedScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consolidated_screening_assessments",
                schema: "governance_intelligence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    proposal_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evaluated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consolidated_screening_assessments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_screening_assessments_case_id_evaluated_at_utc",
                schema: "governance_intelligence",
                table: "consolidated_screening_assessments",
                columns: new[] { "case_id", "evaluated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_screening_assessments_case_id_proposal_version~",
                schema: "governance_intelligence",
                table: "consolidated_screening_assessments",
                columns: new[] { "case_id", "proposal_version", "snapshot_sha256" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Forward-only migration. Preserve assessment history; reverse code registration without dropping this table.");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddColomboStateResearchProjectLeaseReady : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "colombo_state_research_project_lease_ready",
                schema: "land_intelligence",
                columns: table => new
                {
                    ResearchRecordId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DesignationLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DesignationNotice = table.Column<string>(type: "text", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_colombo_state_research_project_lease_ready", x => x.ResearchRecordId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_colombo_state_research_project_lease_ready_DatasetKey",
                schema: "land_intelligence",
                table: "colombo_state_research_project_lease_ready",
                column: "DatasetKey");

            migrationBuilder.CreateIndex(
                name: "IX_colombo_state_research_project_lease_ready_LandParcelId",
                schema: "land_intelligence",
                table: "colombo_state_research_project_lease_ready",
                column: "LandParcelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "colombo_state_research_project_lease_ready",
                schema: "land_intelligence");
        }
    }
}

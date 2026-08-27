using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLandRecommendationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "land_recommendations",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuitabilityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecommendedUseType = table.Column<int>(type: "integer", nullable: true),
                    RecommendedUseDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriteriaJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_land_recommendations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_land_recommendations_GeneratedAt",
                schema: "land_intelligence",
                table: "land_recommendations",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_land_recommendations_LandParcelId",
                schema: "land_intelligence",
                table: "land_recommendations",
                column: "LandParcelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "land_recommendations",
                schema: "land_intelligence");
        }
    }
}

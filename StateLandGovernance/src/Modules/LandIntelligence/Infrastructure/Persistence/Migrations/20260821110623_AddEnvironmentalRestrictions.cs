using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentalRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "environmental_restrictions",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_environmental_restrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_environmental_restrictions_land_parcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_environmental_restrictions_LandParcelId",
                schema: "land_intelligence",
                table: "environmental_restrictions",
                column: "LandParcelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "environmental_restrictions",
                schema: "land_intelligence");
        }
    }
}

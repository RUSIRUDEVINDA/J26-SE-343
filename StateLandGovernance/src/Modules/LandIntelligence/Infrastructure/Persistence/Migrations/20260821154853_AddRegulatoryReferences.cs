using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regulatory_references",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    GazetteNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regulatory_references_land_parcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_references_LandParcelId",
                schema: "land_intelligence",
                table: "regulatory_references",
                column: "LandParcelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regulatory_references",
                schema: "land_intelligence");
        }
    }
}

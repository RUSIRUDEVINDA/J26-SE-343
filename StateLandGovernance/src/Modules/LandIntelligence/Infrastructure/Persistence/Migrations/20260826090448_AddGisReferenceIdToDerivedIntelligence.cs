using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGisReferenceIdToDerivedIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GisReferenceId",
                schema: "land_intelligence",
                table: "infrastructure_features",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GisReferenceId",
                schema: "land_intelligence",
                table: "environmental_restrictions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GisReferenceId",
                schema: "land_intelligence",
                table: "infrastructure_features");

            migrationBuilder.DropColumn(
                name: "GisReferenceId",
                schema: "land_intelligence",
                table: "environmental_restrictions");
        }
    }
}

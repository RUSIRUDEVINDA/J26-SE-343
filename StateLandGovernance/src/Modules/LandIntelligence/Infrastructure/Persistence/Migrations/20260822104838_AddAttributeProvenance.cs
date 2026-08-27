using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttributeProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataProvenanceJson",
                schema: "land_intelligence",
                table: "regulatory_references",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CharacteristicsProvenanceJson",
                schema: "land_intelligence",
                table: "land_parcels",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistanceProvenanceJson",
                schema: "land_intelligence",
                table: "infrastructure_features",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataProvenanceJson",
                schema: "land_intelligence",
                table: "environmental_restrictions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataProvenanceJson",
                schema: "land_intelligence",
                table: "regulatory_references");

            migrationBuilder.DropColumn(
                name: "CharacteristicsProvenanceJson",
                schema: "land_intelligence",
                table: "land_parcels");

            migrationBuilder.DropColumn(
                name: "DistanceProvenanceJson",
                schema: "land_intelligence",
                table: "infrastructure_features");

            migrationBuilder.DropColumn(
                name: "DataProvenanceJson",
                schema: "land_intelligence",
                table: "environmental_restrictions");
        }
    }
}

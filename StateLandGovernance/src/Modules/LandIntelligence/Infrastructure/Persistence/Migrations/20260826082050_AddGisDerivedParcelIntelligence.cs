using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGisDerivedParcelIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "land_parcel_gis_enrichment_snapshots",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallStatus = table.Column<int>(type: "integer", nullable: false),
                    AdministrativeStatus = table.Column<int>(type: "integer", nullable: true),
                    DetectedProvince = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DetectedDistrict = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProvinceMatches = table.Column<bool>(type: "boolean", nullable: true),
                    DistrictMatches = table.Column<bool>(type: "boolean", nullable: true),
                    GeometryBasis = table.Column<int>(type: "integer", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnrichedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_land_parcel_gis_enrichment_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_land_parcel_gis_enrichment_snapshots_land_parcels_LandParce~",
                        column: x => x.LandParcelId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parcel_derived_soil_groups",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoilGroupReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoilGroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OverlapAreaSquareMeters = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OverlapPercentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    GeometryBasis = table.Column<int>(type: "integer", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "jsonb", nullable: false),
                    DerivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcel_derived_soil_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parcel_derived_soil_groups_land_parcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_land_parcel_gis_enrichment_snapshots_LandParcelId",
                schema: "land_intelligence",
                table: "land_parcel_gis_enrichment_snapshots",
                column: "LandParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcel_derived_soil_groups_LandParcelId",
                schema: "land_intelligence",
                table: "parcel_derived_soil_groups",
                column: "LandParcelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "land_parcel_gis_enrichment_snapshots",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "parcel_derived_soil_groups",
                schema: "land_intelligence");
        }
    }
}

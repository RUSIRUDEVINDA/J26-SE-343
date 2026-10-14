using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGisReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gis_administrative_boundaries",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BoundaryType = table.Column<int>(type: "integer", nullable: false),
                    Boundary = table.Column<MultiPolygon>(type: "geometry (MultiPolygon, 4326)", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_administrative_boundaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gis_roads",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RoadType = table.Column<int>(type: "integer", nullable: false),
                    Geometry = table.Column<MultiLineString>(type: "geometry (MultiLineString, 4326)", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_roads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gis_soil_conservation_areas",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Boundary = table.Column<MultiPolygon>(type: "geometry (MultiPolygon, 4326)", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_soil_conservation_areas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gis_soil_erosion_observations",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservationClass = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErosionRate = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    Location = table.Column<Point>(type: "geometry (Point, 4326)", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_soil_erosion_observations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gis_soil_groups",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Boundary = table.Column<MultiPolygon>(type: "geometry (MultiPolygon, 4326)", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_soil_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gis_water_features",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FeatureType = table.Column<int>(type: "integer", nullable: false),
                    Geometry = table.Column<Geometry>(type: "geometry (Geometry, 4326)", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceLayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gis_water_features", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gis_administrative_boundaries_Boundary",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_gis_administrative_boundaries_BoundaryType",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                column: "BoundaryType");

            migrationBuilder.CreateIndex(
                name: "IX_gis_roads_Geometry",
                schema: "land_intelligence",
                table: "gis_roads",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_gis_roads_RoadType",
                schema: "land_intelligence",
                table: "gis_roads",
                column: "RoadType");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_conservation_areas_Boundary",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_erosion_observations_Location",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_groups_Boundary",
                schema: "land_intelligence",
                table: "gis_soil_groups",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_gis_water_features_FeatureType",
                schema: "land_intelligence",
                table: "gis_water_features",
                column: "FeatureType");

            migrationBuilder.CreateIndex(
                name: "IX_gis_water_features_Geometry",
                schema: "land_intelligence",
                table: "gis_water_features",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gis_administrative_boundaries",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "gis_roads",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "gis_soil_conservation_areas",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "gis_soil_erosion_observations",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "gis_soil_groups",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "gis_water_features",
                schema: "land_intelligence");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLandIntelligenceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "land_intelligence");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "land_categories",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_land_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "land_uses",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_land_uses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "land_parcels",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CadastralNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SurveyPlanReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LandCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentLandUseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AreaValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AreaUnit = table.Column<int>(type: "integer", nullable: false),
                    Province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DivisionalSecretariat = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GramaNiladhariDivision = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Centroid = table.Column<Point>(type: "geometry (Point, 4326)", nullable: false),
                    Boundary = table.Column<MultiPolygon>(type: "geometry (MultiPolygon, 4326)", nullable: true),
                    SpatialReferenceSystemId = table.Column<int>(type: "integer", nullable: false),
                    SoilType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TerrainDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ElevationMeters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_land_parcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_land_parcels_land_categories_LandCategoryId",
                        column: x => x.LandCategoryId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_land_parcels_land_uses_CurrentLandUseId",
                        column: x => x.CurrentLandUseId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_uses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "infrastructure_features",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DistanceMeters = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<Point>(type: "geometry (Point, 4326)", nullable: true),
                    SpatialReferenceSystemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_infrastructure_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_infrastructure_features_land_parcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spatial_constraints",
                schema: "land_intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandParcelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    ConstraintGeometry = table.Column<MultiPolygon>(type: "geometry (MultiPolygon, 4326)", nullable: true),
                    SpatialReferenceSystemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spatial_constraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spatial_constraints_land_parcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalSchema: "land_intelligence",
                        principalTable: "land_parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "land_intelligence",
                table: "land_categories",
                columns: new[] { "Id", "Description", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-0001-4000-8000-000000000001"), "[SYNTHETIC] State-owned land category for development/testing.", "State Land", 1 },
                    { new Guid("aaaaaaaa-0001-4000-8000-000000000002"), "[SYNTHETIC] Crown land category for development/testing.", "Crown Land", 2 },
                    { new Guid("aaaaaaaa-0001-4000-8000-000000000003"), "[SYNTHETIC] Reserved land category for development/testing.", "Reserved Land", 3 },
                    { new Guid("aaaaaaaa-0001-4000-8000-000000000099"), "[SYNTHETIC] Other land category for development/testing.", "Other", 99 }
                });

            migrationBuilder.InsertData(
                schema: "land_intelligence",
                table: "land_uses",
                columns: new[] { "Id", "Description", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000001"), "[SYNTHETIC]", "Agricultural", 1 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000002"), "[SYNTHETIC]", "Commercial", 2 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000003"), "[SYNTHETIC]", "Industrial", 3 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000004"), "[SYNTHETIC]", "Residential", 4 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000005"), "[SYNTHETIC]", "Tourism", 5 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000006"), "[SYNTHETIC]", "Conservation", 6 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000007"), "[SYNTHETIC]", "Mixed Use", 7 },
                    { new Guid("bbbbbbbb-0001-4000-8000-000000000099"), "[SYNTHETIC]", "Other", 99 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_infrastructure_features_LandParcelId",
                schema: "land_intelligence",
                table: "infrastructure_features",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_infrastructure_features_Location",
                schema: "land_intelligence",
                table: "infrastructure_features",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_land_categories_Type",
                schema: "land_intelligence",
                table: "land_categories",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_land_parcels_Boundary",
                schema: "land_intelligence",
                table: "land_parcels",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_land_parcels_CadastralNumber",
                schema: "land_intelligence",
                table: "land_parcels",
                column: "CadastralNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_land_parcels_Centroid",
                schema: "land_intelligence",
                table: "land_parcels",
                column: "Centroid")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_land_parcels_CurrentLandUseId",
                schema: "land_intelligence",
                table: "land_parcels",
                column: "CurrentLandUseId");

            migrationBuilder.CreateIndex(
                name: "IX_land_parcels_LandCategoryId",
                schema: "land_intelligence",
                table: "land_parcels",
                column: "LandCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_land_uses_Type",
                schema: "land_intelligence",
                table: "land_uses",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spatial_constraints_ConstraintGeometry",
                schema: "land_intelligence",
                table: "spatial_constraints",
                column: "ConstraintGeometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_spatial_constraints_LandParcelId",
                schema: "land_intelligence",
                table: "spatial_constraints",
                column: "LandParcelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "infrastructure_features",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "spatial_constraints",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "land_parcels",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "land_categories",
                schema: "land_intelligence");

            migrationBuilder.DropTable(
                name: "land_uses",
                schema: "land_intelligence");
        }
    }
}

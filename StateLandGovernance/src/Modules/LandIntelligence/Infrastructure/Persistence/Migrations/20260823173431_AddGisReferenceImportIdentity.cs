using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGisReferenceImportIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_water_features",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_water_features",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_groups",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_groups",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_roads",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_roads",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gis_water_features_SourceName_SourceLayer_SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_water_features",
                columns: new[] { "SourceName", "SourceLayer", "SourceFeatureId" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_water_features_SourceName_SourceLayer_SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_water_features",
                columns: new[] { "SourceName", "SourceLayer", "SourceFingerprint" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gis_water_features_source_identity",
                schema: "land_intelligence",
                table: "gis_water_features",
                sql: "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_groups_SourceName_SourceLayer_SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_groups",
                columns: new[] { "SourceName", "SourceLayer", "SourceFeatureId" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_groups_SourceName_SourceLayer_SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_groups",
                columns: new[] { "SourceName", "SourceLayer", "SourceFingerprint" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gis_soil_groups_source_identity",
                schema: "land_intelligence",
                table: "gis_soil_groups",
                sql: "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_erosion_observations_SourceName_SourceLayer_Sourc~1",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations",
                columns: new[] { "SourceName", "SourceLayer", "SourceFingerprint" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_erosion_observations_SourceName_SourceLayer_Source~",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations",
                columns: new[] { "SourceName", "SourceLayer", "SourceFeatureId" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gis_soil_erosion_observations_source_identity",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations",
                sql: "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_conservation_areas_SourceName_SourceLayer_SourceFe~",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas",
                columns: new[] { "SourceName", "SourceLayer", "SourceFeatureId" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_soil_conservation_areas_SourceName_SourceLayer_SourceFi~",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas",
                columns: new[] { "SourceName", "SourceLayer", "SourceFingerprint" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gis_soil_conservation_areas_source_identity",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas",
                sql: "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_roads_SourceName_SourceLayer_SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_roads",
                columns: new[] { "SourceName", "SourceLayer", "SourceFeatureId" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_roads_SourceName_SourceLayer_SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_roads",
                columns: new[] { "SourceName", "SourceLayer", "SourceFingerprint" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gis_roads_source_identity",
                schema: "land_intelligence",
                table: "gis_roads",
                sql: "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_administrative_boundaries_SourceName_SourceLayer_Sourc~1",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                columns: new[] { "SourceName", "SourceLayer", "SourceFingerprint" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_gis_administrative_boundaries_SourceName_SourceLayer_Source~",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                columns: new[] { "SourceName", "SourceLayer", "SourceFeatureId" },
                unique: true,
                filter: "\"SourceFeatureId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_gis_administrative_boundaries_source_identity",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries",
                sql: "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_gis_water_features_SourceName_SourceLayer_SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_water_features");

            migrationBuilder.DropIndex(
                name: "IX_gis_water_features_SourceName_SourceLayer_SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_water_features");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gis_water_features_source_identity",
                schema: "land_intelligence",
                table: "gis_water_features");

            migrationBuilder.DropIndex(
                name: "IX_gis_soil_groups_SourceName_SourceLayer_SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_groups");

            migrationBuilder.DropIndex(
                name: "IX_gis_soil_groups_SourceName_SourceLayer_SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_groups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gis_soil_groups_source_identity",
                schema: "land_intelligence",
                table: "gis_soil_groups");

            migrationBuilder.DropIndex(
                name: "IX_gis_soil_erosion_observations_SourceName_SourceLayer_Sourc~1",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations");

            migrationBuilder.DropIndex(
                name: "IX_gis_soil_erosion_observations_SourceName_SourceLayer_Source~",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gis_soil_erosion_observations_source_identity",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations");

            migrationBuilder.DropIndex(
                name: "IX_gis_soil_conservation_areas_SourceName_SourceLayer_SourceFe~",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas");

            migrationBuilder.DropIndex(
                name: "IX_gis_soil_conservation_areas_SourceName_SourceLayer_SourceFi~",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gis_soil_conservation_areas_source_identity",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas");

            migrationBuilder.DropIndex(
                name: "IX_gis_roads_SourceName_SourceLayer_SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_roads");

            migrationBuilder.DropIndex(
                name: "IX_gis_roads_SourceName_SourceLayer_SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_roads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gis_roads_source_identity",
                schema: "land_intelligence",
                table: "gis_roads");

            migrationBuilder.DropIndex(
                name: "IX_gis_administrative_boundaries_SourceName_SourceLayer_Sourc~1",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries");

            migrationBuilder.DropIndex(
                name: "IX_gis_administrative_boundaries_SourceName_SourceLayer_Source~",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gis_administrative_boundaries_source_identity",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries");

            migrationBuilder.DropColumn(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_water_features");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_water_features");

            migrationBuilder.DropColumn(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_groups");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_groups");

            migrationBuilder.DropColumn(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_erosion_observations");

            migrationBuilder.DropColumn(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_soil_conservation_areas");

            migrationBuilder.DropColumn(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_roads");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_roads");

            migrationBuilder.DropColumn(
                name: "SourceFeatureId",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "land_intelligence",
                table: "gis_administrative_boundaries");
        }
    }
}

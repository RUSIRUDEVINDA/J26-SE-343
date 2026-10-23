using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(LandIntelligenceDbContext))]
[Migration("20261021120000_AddParcelGnDivisionMappings")]
public partial class AddParcelGnDivisionMappings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "parcel_gn_division_mappings",
            schema: "land_intelligence",
            columns: table => new
            {
                CadastralNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                LandParcelId = table.Column<Guid>(type: "uuid", nullable: true),
                DatasetKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                SourceGnIndex = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                WorkbookStatusLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SourceCodeGnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SourceCodeGnNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                SourceCodeDs = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                CodeDistrict = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                MarkerGnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                MarkerGnNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                MarkerDs = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                MarkerLatitude = table.Column<double>(type: "double precision", nullable: true),
                MarkerLongitude = table.Column<double>(type: "double precision", nullable: true),
                WorkbookSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ImportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_parcel_gn_division_mappings", x => x.CadastralNumber);
            });

        migrationBuilder.CreateIndex(
            name: "IX_parcel_gn_division_mappings_LandParcelId",
            schema: "land_intelligence",
            table: "parcel_gn_division_mappings",
            column: "LandParcelId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "parcel_gn_division_mappings",
            schema: "land_intelligence");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations;

/// <summary>
/// Geography cast KNN (<c>ORDER BY geom::geography &lt;-&gt; …</c>) cannot use the
/// existing geometry GIST index. This expression index is optional acceleration;
/// correctness still uses ST_Distance geography as the final metric.
/// </summary>
[DbContext(typeof(LandIntelligenceDbContext))]
[Migration("20260920153000_AddGisRoadsGeographyExpressionIndex")]
public partial class AddGisRoadsGeographyExpressionIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS land_intelligence."IX_gis_roads_Geometry_geography";
            CREATE INDEX IF NOT EXISTS "IX_gis_roads_osm_Geometry_geography"
            ON land_intelligence.gis_roads
            USING GIST (("Geometry"::geography))
            WHERE "SourceLayer" = 'osm_motor_roads';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS land_intelligence."IX_gis_roads_osm_Geometry_geography";
            DROP INDEX IF EXISTS land_intelligence."IX_gis_roads_Geometry_geography";
            """);
    }
}

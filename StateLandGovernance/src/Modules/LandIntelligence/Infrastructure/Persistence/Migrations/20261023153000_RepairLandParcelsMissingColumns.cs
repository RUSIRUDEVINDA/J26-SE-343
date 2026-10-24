using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

#nullable disable

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Migrations;

/// <summary>
/// Repairs partial imports where __ef_migrations_history was applied without all column DDL
/// (e.g. missing land_parcels characteristic / provenance columns).
/// </summary>
[DbContext(typeof(LandIntelligenceDbContext))]
[Migration("20261023153000_RepairLandParcelsMissingColumns")]
public partial class RepairLandParcelsMissingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE land_intelligence.land_parcels
                ADD COLUMN IF NOT EXISTS "SoilType" character varying(100);

            ALTER TABLE land_intelligence.land_parcels
                ADD COLUMN IF NOT EXISTS "TerrainDescription" character varying(500);

            ALTER TABLE land_intelligence.land_parcels
                ADD COLUMN IF NOT EXISTS "ElevationMeters" numeric(10,2);

            ALTER TABLE land_intelligence.land_parcels
                ADD COLUMN IF NOT EXISTS "CharacteristicsProvenanceJson" jsonb;

            CREATE TABLE IF NOT EXISTS land_intelligence.parcel_derived_soil_groups (
                "Id" uuid NOT NULL,
                "LandParcelId" uuid NOT NULL,
                "SoilGroupReferenceId" uuid NOT NULL,
                "SoilGroupName" character varying(200) NOT NULL,
                "OverlapAreaSquareMeters" numeric(18,4) NULL,
                "OverlapPercentage" numeric(8,4) NULL,
                "GeometryBasis" integer NULL,
                "SourceName" character varying(200) NOT NULL,
                "SourceLayer" character varying(200) NOT NULL,
                "ProvenanceJson" jsonb NOT NULL,
                "DerivedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_parcel_derived_soil_groups" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_parcel_derived_soil_groups_land_parcels_LandParcelId"
                    FOREIGN KEY ("LandParcelId")
                    REFERENCES land_intelligence.land_parcels ("Id")
                    ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_parcel_derived_soil_groups_LandParcelId"
                ON land_intelligence.parcel_derived_soil_groups ("LandParcelId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS land_intelligence.parcel_derived_soil_groups;

            ALTER TABLE land_intelligence.land_parcels
                DROP COLUMN IF EXISTS "CharacteristicsProvenanceJson";

            ALTER TABLE land_intelligence.land_parcels
                DROP COLUMN IF EXISTS "ElevationMeters";

            ALTER TABLE land_intelligence.land_parcels
                DROP COLUMN IF EXISTS "TerrainDescription";

            ALTER TABLE land_intelligence.land_parcels
                DROP COLUMN IF EXISTS "SoilType";
            """);
    }
}

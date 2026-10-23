using Microsoft.EntityFrameworkCore;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

/// <summary>
/// Idempotent repairs when migration history was recorded without full DDL (partial research imports).
/// </summary>
public static class LandIntelligenceSchemaRepair
{
    public static async Task ApplyAsync(
        LandIntelligenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
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

            ALTER TABLE land_intelligence.lease_interest_requests
                ADD COLUMN IF NOT EXISTS "ApplicantBrnLookupDigest" character varying(64);

            CREATE INDEX IF NOT EXISTS "IX_lease_interest_requests_ApplicantBrnLookupDigest"
                ON land_intelligence.lease_interest_requests ("ApplicantBrnLookupDigest");
            """,
            cancellationToken);
    }
}

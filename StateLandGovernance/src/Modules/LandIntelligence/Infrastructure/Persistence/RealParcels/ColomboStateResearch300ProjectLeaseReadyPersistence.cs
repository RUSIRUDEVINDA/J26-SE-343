using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

internal static class ColomboStateResearch300ProjectLeaseReadyPersistence
{
    public const string UpsertSql = """
        INSERT INTO land_intelligence.colombo_state_research_project_lease_ready
        ("ResearchRecordId","LandParcelId","DatasetKey","DesignationLabel","DesignationNotice","ImportedAtUtc")
        VALUES (@recordId,@parcelId,@datasetKey,@label,@notice,now())
        ON CONFLICT ("ResearchRecordId") DO UPDATE SET
        "LandParcelId"=EXCLUDED."LandParcelId",
        "DatasetKey"=EXCLUDED."DatasetKey",
        "DesignationLabel"=EXCLUDED."DesignationLabel",
        "DesignationNotice"=EXCLUDED."DesignationNotice",
        "ImportedAtUtc"=now();
        """;

    public const string EnsureTableSql = """
        CREATE TABLE IF NOT EXISTS land_intelligence.colombo_state_research_project_lease_ready (
            "ResearchRecordId" varchar(120) PRIMARY KEY,
            "LandParcelId" uuid NOT NULL REFERENCES land_intelligence.land_parcels("Id") ON DELETE CASCADE,
            "DatasetKey" varchar(80) NOT NULL,
            "DesignationLabel" varchar(200) NOT NULL,
            "DesignationNotice" text NOT NULL,
            "ImportedAtUtc" timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_colombo_state_research_project_lease_ready_parcel
            ON land_intelligence.colombo_state_research_project_lease_ready ("LandParcelId");
        CREATE INDEX IF NOT EXISTS ix_colombo_state_research_project_lease_ready_dataset
            ON land_intelligence.colombo_state_research_project_lease_ready ("DatasetKey");
        """;

    public static (string Label, string Notice) DesignationCopy() =>
        (ColomboStateResearchProjectLeaseReady.DesignationLabel, ColomboStateResearchProjectLeaseReady.DesignationNotice);
}

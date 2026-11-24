-- REVIEW ONLY. NOT EXECUTED. EXACT FOUR ADDITIONAL RUNTIME PRIVILEGE ENTRIES.
GRANT SELECT, INSERT ON TABLE governance_intelligence.officer_investigation_attachments TO slg_c4_regulatory_runtime;
GRANT SELECT, INSERT ON TABLE governance_intelligence.assessment_journey_events TO slg_c4_regulatory_runtime;
-- No UPDATE/DELETE/TRUNCATE/DDL/schema/sequence/account/peer/outbox/role changes.
-- No extra privileges on regulatory_followup_events: existing SELECT/INSERT suffice.
-- Creating tables/replacing the CHECK requires the separate operator, never runtime elevation.

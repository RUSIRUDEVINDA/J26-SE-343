-- REVIEW ONLY: separate ordinary activation approval required.
BEGIN;
GRANT SELECT, INSERT ON governance_intelligence.complaint_assignment_events TO slg_c4_regulatory_runtime;
GRANT SELECT, INSERT ON governance_intelligence.complaint_handling_events TO slg_c4_regulatory_runtime;
COMMIT;

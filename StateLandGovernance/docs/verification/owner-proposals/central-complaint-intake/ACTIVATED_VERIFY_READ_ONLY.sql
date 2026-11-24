BEGIN READ ONLY;
SELECT current_database(), current_setting('transaction_read_only');
SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY "MigrationId";
SELECT count(*) AS assignments FROM governance_intelligence.complaint_assignment_events;
SELECT count(*) AS handling_events FROM governance_intelligence.complaint_handling_events;
COMMIT;

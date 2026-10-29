-- Use ONLY in the isolated restored copy now, or after separately approved ordinary deployment.
-- This table is not present in the ordinary database yet.
BEGIN READ ONLY;
SET LOCAL timezone='UTC';
SELECT current_database(),inet_server_port(),current_setting('transaction_read_only');
SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY "MigrationId";
SELECT "Id","EvaluationId","RuleCode","RuleVersion","Operation","ReviewOutcome",
       "Status","Revision","PreviousEventId","CreatedAtUtc","InputSha256","EventSha256"
FROM governance_intelligence.regulatory_followup_events ORDER BY "EvaluationId","RuleCode","Revision";
-- Omits officer IDs, private remarks/references and original input contents.
SELECT "Id","Status","RuleSetVersion","EvaluationTimestamp"
FROM governance_intelligence.compliance_evaluations ORDER BY "EvaluationTimestamp" DESC;
SELECT grantee,privilege_type FROM information_schema.table_privileges
WHERE table_schema='governance_intelligence' AND table_name='regulatory_followup_events'
ORDER BY grantee,privilege_type;
COMMIT;

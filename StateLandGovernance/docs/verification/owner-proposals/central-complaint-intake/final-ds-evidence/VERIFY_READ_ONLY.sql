BEGIN READ ONLY;
SELECT current_database(),current_user,current_setting('transaction_read_only');
SELECT oid,datname FROM pg_database WHERE datname=current_database();
SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY "MigrationId";
SELECT "Id","RequestId","Concern","CreatorActorId","CreatedAtUtc","FileName","ContentType",octet_length("Content") AS bytes,"ContentSha256" FROM governance_intelligence.screening_evidence_attachments ORDER BY "CreatedAtUtc";
SELECT "Id","RequestId","Concern","Revision","PreviousEventId","PreviousStatus","Status","CreatorActorId","CreatedAtUtc","EventSha256" FROM governance_intelligence.screening_evidence_verifications ORDER BY "RequestId","Concern","Revision";
-- Remarks, reference payloads and original evidence bytes are intentionally omitted from this metadata check.
COMMIT;

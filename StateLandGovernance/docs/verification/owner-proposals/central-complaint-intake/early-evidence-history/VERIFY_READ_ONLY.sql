BEGIN READ ONLY;
SELECT current_database(),current_user,current_setting('transaction_read_only');
SELECT oid FROM pg_database WHERE datname=current_database();
SELECT count(*) AS migrations FROM governance_intelligence.__ef_migrations_history;
SELECT r."Id" AS request,r."EarlyAssessmentId",i."ProposalId",r."CreatedAtUtc",
 (SELECT count(*) FROM governance_intelligence.screening_evidence_attachments a WHERE a."RequestId"=r."Id") AS attachments,
 (SELECT count(*) FROM governance_intelligence.screening_evidence_verifications v WHERE v."RequestId"=r."Id") AS decisions
FROM governance_intelligence.officer_assessment_requests r JOIN governance_intelligence.officer_investigations i ON i."Id"=r."InvestigationId"
WHERE r."Id" IN ('284e96a3-6744-45b4-8777-7f062336f7d5','3ae73d0e-0271-485a-b307-52d603019679');
SELECT "Id","RequestId","Concern","FileName",octet_length("Content") AS bytes,"ContentSha256",encode(sha256("Content"),'hex')="ContentSha256" AS hash_matches FROM governance_intelligence.screening_evidence_attachments WHERE "Id"='c74e25f6-a006-4988-9998-485a887b0aae';
SELECT "Id","RequestId","Concern","Revision","PreviousStatus","Status","CreatedAtUtc","EventSha256" FROM governance_intelligence.screening_evidence_verifications WHERE "RequestId" IN ('284e96a3-6744-45b4-8777-7f062336f7d5','3ae73d0e-0271-485a-b307-52d603019679') ORDER BY "RequestId","Concern","Revision";
ROLLBACK;

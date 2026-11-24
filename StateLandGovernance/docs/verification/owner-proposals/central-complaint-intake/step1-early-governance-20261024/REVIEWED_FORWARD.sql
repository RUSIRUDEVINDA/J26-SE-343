START TRANSACTION;

ALTER TABLE governance_intelligence.officer_action_events DROP CONSTRAINT ck_officer_operation;

CREATE TABLE governance_intelligence.officer_investigation_attachments (
    "Id" uuid NOT NULL,
    "InvestigationId" uuid NOT NULL,
    "CreatorActorId" uuid NOT NULL,
    "SubmissionKey" uuid NOT NULL,
    "Concern" character varying(100) NOT NULL,
    "FileName" character varying(150) NOT NULL,
    "ContentType" character varying(50) NOT NULL,
    "Content" bytea NOT NULL,
    "ContentSha256" character varying(64) NOT NULL,
    "InputSha256" character varying(64) NOT NULL,
    "CreatedAtUtc" timestamp(6) with time zone NOT NULL,
    CONSTRAINT pk_investigation_attachments PRIMARY KEY ("Id"),
    CONSTRAINT uq_investigation_attachment_submission UNIQUE ("CreatorActorId", "SubmissionKey"),
    CONSTRAINT ck_investigation_attachment_bytes CHECK (octet_length("Content") BETWEEN 1 AND 5242880),
    CONSTRAINT ck_investigation_attachment_concern CHECK ("Concern" IN ('LegalDispute','UnauthorizedOccupation','UnauthorizedConstruction','FamilyOrInheritanceClaim','MultipleClaimants','UnresolvedObjection','PreviousIllegalLandActivity')),
    CONSTRAINT ck_investigation_attachment_content_hash CHECK (encode(sha256("Content"),'hex')="ContentSha256"),
    CONSTRAINT ck_investigation_attachment_filename CHECK (length(btrim("FileName")) BETWEEN 1 AND 150 AND position('/' in "FileName")=0 AND position(chr(92) in "FileName")=0 AND position(':' in "FileName")=0 AND "FileName" !~ '[[:cntrl:]]'),
    CONSTRAINT ck_investigation_attachment_hashes CHECK ("ContentSha256" ~ '^[0-9a-f]{64}$' AND "InputSha256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_investigation_attachment_identity CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'::uuid AND "CreatorActorId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "SubmissionKey" <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_investigation_attachment_type CHECK ("ContentType" IN ('application/pdf','image/png','image/jpeg','text/plain')),
    CONSTRAINT fk_investigation_attachment_owner FOREIGN KEY ("InvestigationId", "CreatorActorId") REFERENCES governance_intelligence.officer_investigations ("Id", "CreatorActorId") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX ux_officer_investigation_finalization ON governance_intelligence.officer_action_events ("InvestigationId") WHERE "Operation" = 'InvestigationFinalized';

ALTER TABLE governance_intelligence.officer_action_events ADD CONSTRAINT ck_officer_operation CHECK ("Operation" IN ('InvestigationSaved','RequestSubmitted','InferenceStarted','AssessmentSaved','AssessmentFailed','OutcomeUnknown') OR ("Operation"='InvestigationFinalized' AND "InvestigationId" IS NOT NULL AND "RequestId" IS NULL));

CREATE INDEX ix_investigation_attachment_history ON governance_intelligence.officer_investigation_attachments ("InvestigationId", "CreatedAtUtc", "Id");

INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261024000805_AddInvestigationEvidenceAndFinalization', '8.0.2');

COMMIT;


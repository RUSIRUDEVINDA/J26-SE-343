START TRANSACTION;

CREATE TABLE governance_intelligence.screening_evidence_attachments (
    "Id" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "CreatorActorId" uuid NOT NULL,
    "SubmissionKey" uuid NOT NULL,
    "Concern" character varying(100) NOT NULL,
    "FileName" character varying(150) NOT NULL,
    "ContentType" character varying(50) NOT NULL,
    "Content" bytea NOT NULL,
    "ContentSha256" character varying(64) NOT NULL,
    "InputSha256" character varying(64) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_screening_evidence_attachments" PRIMARY KEY ("Id"),
    CONSTRAINT ck_screening_attachment_bytes CHECK (octet_length("Content") BETWEEN 1 AND 5242880),
    CONSTRAINT ck_screening_attachment_hashes CHECK (length("ContentSha256")=64 AND length("InputSha256")=64),
    CONSTRAINT ck_screening_attachment_type CHECK ("ContentType" IN ('application/pdf','image/png','image/jpeg','text/plain')),
    CONSTRAINT "FK_screening_evidence_attachments_officer_assessment_requests_~" FOREIGN KEY ("RequestId", "CreatorActorId") REFERENCES governance_intelligence.officer_assessment_requests ("Id", "CreatorActorId") ON DELETE RESTRICT
);

CREATE TABLE governance_intelligence.screening_evidence_verifications (
    "Id" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "CreatorActorId" uuid NOT NULL,
    "SubmissionKey" uuid NOT NULL,
    "Concern" character varying(100) NOT NULL,
    "Revision" integer NOT NULL,
    "PreviousEventId" uuid,
    "PreviousStatus" character varying(32) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Remarks" character varying(2000) NOT NULL,
    "AttachmentIdsJson" jsonb NOT NULL,
    "ReferencesJson" jsonb NOT NULL,
    "InputSha256" character varying(64) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CanonicalJson" text NOT NULL,
    "EventSha256" character varying(64) NOT NULL,
    CONSTRAINT "PK_screening_evidence_verifications" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_screening_evidence_verifications_Id_RequestId_Concern_Creat~" UNIQUE ("Id", "RequestId", "Concern", "CreatorActorId"),
    CONSTRAINT ck_screening_verification_hash CHECK (length("InputSha256")=64 AND length("EventSha256")=64),
    CONSTRAINT ck_screening_verification_refs CHECK (jsonb_typeof("AttachmentIdsJson")='array' AND jsonb_typeof("ReferencesJson")='array' AND jsonb_array_length("AttachmentIdsJson")<=10 AND jsonb_array_length("ReferencesJson")<=10),
    CONSTRAINT ck_screening_verification_remarks CHECK (length("Remarks") BETWEEN 20 AND 2000),
    CONSTRAINT ck_screening_verification_revision CHECK ("Revision">0 AND (("Revision"=1 AND "PreviousEventId" IS NULL AND "PreviousStatus"='Unverified') OR ("Revision">1 AND "PreviousEventId" IS NOT NULL))),
    CONSTRAINT ck_screening_verification_status CHECK ("Status" IN ('Verified','Rejected','MoreInformationRequired') AND "PreviousStatus" IN ('Unverified','Verified','Rejected','MoreInformationRequired')),
    CONSTRAINT "FK_screening_evidence_verifications_officer_assessment_request~" FOREIGN KEY ("RequestId", "CreatorActorId") REFERENCES governance_intelligence.officer_assessment_requests ("Id", "CreatorActorId") ON DELETE RESTRICT,
    CONSTRAINT "FK_screening_evidence_verifications_screening_evidence_verific~" FOREIGN KEY ("PreviousEventId", "RequestId", "Concern", "CreatorActorId") REFERENCES governance_intelligence.screening_evidence_verifications ("Id", "RequestId", "Concern", "CreatorActorId") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_screening_evidence_attachments_CreatorActorId_SubmissionKey" ON governance_intelligence.screening_evidence_attachments ("CreatorActorId", "SubmissionKey");

CREATE INDEX "IX_screening_evidence_attachments_RequestId_CreatedAtUtc" ON governance_intelligence.screening_evidence_attachments ("RequestId", "CreatedAtUtc");

CREATE INDEX "IX_screening_evidence_attachments_RequestId_CreatorActorId" ON governance_intelligence.screening_evidence_attachments ("RequestId", "CreatorActorId");

CREATE UNIQUE INDEX "IX_screening_evidence_verifications_CreatorActorId_SubmissionK~" ON governance_intelligence.screening_evidence_verifications ("CreatorActorId", "SubmissionKey");

CREATE UNIQUE INDEX "IX_screening_evidence_verifications_PreviousEventId" ON governance_intelligence.screening_evidence_verifications ("PreviousEventId");

CREATE INDEX "IX_screening_evidence_verifications_PreviousEventId_RequestId_~" ON governance_intelligence.screening_evidence_verifications ("PreviousEventId", "RequestId", "Concern", "CreatorActorId");

CREATE UNIQUE INDEX "IX_screening_evidence_verifications_RequestId_Concern_Revision" ON governance_intelligence.screening_evidence_verifications ("RequestId", "Concern", "Revision");

CREATE INDEX "IX_screening_evidence_verifications_RequestId_CreatorActorId" ON governance_intelligence.screening_evidence_verifications ("RequestId", "CreatorActorId");

INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261023093022_AddScreeningEvidenceVerification', '8.0.2');

COMMIT;


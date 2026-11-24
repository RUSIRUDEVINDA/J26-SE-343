START TRANSACTION;

CREATE TABLE governance_intelligence.regulatory_followup_events (
    "Id" uuid NOT NULL,
    "EvaluationId" uuid NOT NULL,
    "RuleCode" character varying(100) NOT NULL,
    "RuleVersion" character varying(100) NOT NULL,
    "ActorId" uuid NOT NULL,
    "SubmissionKey" uuid NOT NULL,
    "Operation" character varying(32) NOT NULL,
    "ReviewOutcome" character varying(48),
    "MissingInformation" character varying(2000),
    "Remarks" character varying(2000) NOT NULL,
    "ReferencesJson" jsonb NOT NULL,
    "Status" character varying(16) NOT NULL,
    "Revision" integer NOT NULL,
    "PreviousEventId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "InputSha256" character varying(64) NOT NULL,
    "CanonicalJson" text NOT NULL,
    "EventSha256" character varying(64) NOT NULL,
    CONSTRAINT "PK_regulatory_followup_events" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_regulatory_followup_events_Id_EvaluationId_RuleCode" UNIQUE ("Id", "EvaluationId", "RuleCode"),
    CONSTRAINT ck_regulatory_followup_hash CHECK ("InputSha256" ~ '^[0-9a-f]{64}$' AND "EventSha256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_regulatory_followup_operation CHECK (("Operation"='ClarificationRequested' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NOT NULL AND length("MissingInformation") BETWEEN 20 AND 2000) OR ("Operation"='HumanReviewRecorded' AND "MissingInformation" IS NULL AND "ReviewOutcome" IS NOT NULL AND "ReviewOutcome" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview'))),
    CONSTRAINT ck_regulatory_followup_refs CHECK (jsonb_typeof("ReferencesJson")='array' AND jsonb_array_length("ReferencesJson")<=10 AND NOT jsonb_path_exists("ReferencesJson", '$[*] ? (@.type() != "string")')),
    CONSTRAINT ck_regulatory_followup_remarks CHECK (length("Remarks") BETWEEN 20 AND 2000),
    CONSTRAINT ck_regulatory_followup_revision CHECK (("Revision"=1 AND "PreviousEventId" IS NULL) OR ("Revision">1 AND "PreviousEventId" IS NOT NULL)),
    CONSTRAINT ck_regulatory_followup_rule CHECK (length("RuleCode")>0 AND length("RuleVersion")>0),
    CONSTRAINT ck_regulatory_followup_status CHECK ("Status"='Recorded'),
    CONSTRAINT "FK_regulatory_followup_events_compliance_evaluations_Evaluatio~" FOREIGN KEY ("EvaluationId") REFERENCES governance_intelligence.compliance_evaluations ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_regulatory_followup_events_regulatory_followup_events_Previ~" FOREIGN KEY ("PreviousEventId", "EvaluationId", "RuleCode") REFERENCES governance_intelligence.regulatory_followup_events ("Id", "EvaluationId", "RuleCode") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_regulatory_followup_events_ActorId_SubmissionKey" ON governance_intelligence.regulatory_followup_events ("ActorId", "SubmissionKey");

CREATE INDEX "IX_regulatory_followup_events_EvaluationId_CreatedAtUtc" ON governance_intelligence.regulatory_followup_events ("EvaluationId", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_regulatory_followup_events_EvaluationId_RuleCode_Revision" ON governance_intelligence.regulatory_followup_events ("EvaluationId", "RuleCode", "Revision");

CREATE UNIQUE INDEX "IX_regulatory_followup_events_PreviousEventId" ON governance_intelligence.regulatory_followup_events ("PreviousEventId");

CREATE INDEX "IX_regulatory_followup_events_PreviousEventId_EvaluationId_Rul~" ON governance_intelligence.regulatory_followup_events ("PreviousEventId", "EvaluationId", "RuleCode");

INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261023121903_AddRegulatoryFollowupEvents', '8.0.2');

COMMIT;


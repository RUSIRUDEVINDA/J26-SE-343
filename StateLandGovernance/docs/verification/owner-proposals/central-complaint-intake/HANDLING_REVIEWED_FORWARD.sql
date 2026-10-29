START TRANSACTION;

CREATE TABLE governance_intelligence.complaint_handling_events (
    "Id" uuid NOT NULL,
    "ComplaintId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "PreviousEventId" uuid,
    "Operation" character varying(32) NOT NULL,
    "FromState" character varying(32) NOT NULL,
    "ToState" character varying(32) NOT NULL,
    "HandlingMode" character varying(16) NOT NULL,
    "ResponsibleActorId" uuid,
    "AssignmentEventId" uuid,
    "ActorId" uuid NOT NULL,
    "ActedAtUtc" timestamp with time zone NOT NULL,
    "Reason" character varying(2000) NOT NULL,
    "DetailsJson" jsonb NOT NULL,
    "EvidenceReferencesJson" jsonb NOT NULL,
    "PublicSummary" character varying(2000) NOT NULL,
    "SubmissionKey" uuid NOT NULL,
    "InputSha256" character varying(64) NOT NULL,
    "AuthorizationGrantId" uuid,
    "AuthorizationSourceReference" character varying(500),
    "EventCanonicalJson" text NOT NULL,
    "EventSha256" character varying(64) NOT NULL,
    CONSTRAINT "PK_complaint_handling_events" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_complaint_handling_event_parent" UNIQUE ("Id", "ComplaintId"),
    CONSTRAINT "UQ_complaint_handling_revision" UNIQUE ("ComplaintId", "Revision"),
    CONSTRAINT "UQ_complaint_handling_submission" UNIQUE ("ActorId", "SubmissionKey"),
    CONSTRAINT "CK_complaint_handling_authority" CHECK (
  ("AuthorizationGrantId" IS NULL AND "AuthorizationSourceReference" IS NULL AND "HandlingMode"='Ds' AND
   "Operation" IN ('StartInvestigation','RecordNote','RequestInformation','RecordFindings','DsReviewRecorded')) OR
  ("AuthorizationGrantId" IS NOT NULL AND "AuthorizationSourceReference" IS NOT NULL AND length(btrim("AuthorizationSourceReference"))>0)),
    CONSTRAINT "CK_complaint_handling_canonical" CHECK (length(btrim("EventCanonicalJson"))>0),
    CONSTRAINT "CK_complaint_handling_details" CHECK (jsonb_typeof("DetailsJson")='object' AND octet_length("DetailsJson"::text)<=32768),
    CONSTRAINT "CK_complaint_handling_evidence" CHECK (
  CASE WHEN jsonb_typeof("EvidenceReferencesJson")='array' THEN jsonb_array_length("EvidenceReferencesJson")<=20 ELSE false END),
    CONSTRAINT "CK_complaint_handling_hashes" CHECK (
  "InputSha256" ~ '^[0-9a-f]{64}$' AND "EventSha256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "CK_complaint_handling_operation" CHECK ("Operation" IN
  ('TakeDirect','AssignDs','ReassignDs','StartInvestigation','RecordNote','RequestInformation','RecordFindings','Resolve','Close','Reopen','DsReviewRecorded')),
    CONSTRAINT "CK_complaint_handling_reason" CHECK (length(btrim("Reason"))>0),
    CONSTRAINT "CK_complaint_handling_responsibility" CHECK (
  ("HandlingMode"='Commissioner' AND "ResponsibleActorId" IS NOT NULL AND "AssignmentEventId" IS NULL AND
   "ToState" IN ('UnderCommissionerReview','InformationRequested','UnderInvestigation','Reopened')) OR
  ("HandlingMode"='Ds' AND "ResponsibleActorId" IS NOT NULL AND "AssignmentEventId" IS NOT NULL AND
   "ToState" IN ('AssignedToDs','InformationRequested','UnderInvestigation')) OR
  ("HandlingMode"='None' AND "ResponsibleActorId" IS NULL AND "AssignmentEventId" IS NULL AND "ToState" IN ('Resolved','Closed'))),
    CONSTRAINT "CK_complaint_handling_revision" CHECK (
  ("Revision"=1 AND "PreviousEventId" IS NULL) OR ("Revision">1 AND "PreviousEventId" IS NOT NULL)),
    CONSTRAINT "CK_complaint_handling_states" CHECK (
  "FromState" IN ('Unassigned','UnderCommissionerReview','AssignedToDs','InformationRequested','UnderInvestigation','Resolved','Closed','Reopened') AND
  "ToState" IN ('Unassigned','UnderCommissionerReview','AssignedToDs','InformationRequested','UnderInvestigation','Resolved','Closed','Reopened')),
    CONSTRAINT "FK_complaint_handling_assignment" FOREIGN KEY ("AssignmentEventId", "ComplaintId") REFERENCES governance_intelligence.complaint_assignment_events ("Id", "ComplaintId") ON DELETE RESTRICT,
    CONSTRAINT "FK_complaint_handling_complaint" FOREIGN KEY ("ComplaintId") REFERENCES governance_intelligence.registered_complaints ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_complaint_handling_previous" FOREIGN KEY ("PreviousEventId", "ComplaintId") REFERENCES governance_intelligence.complaint_handling_events ("Id", "ComplaintId") ON DELETE RESTRICT
);

CREATE INDEX ix_complaint_handling_responsible ON governance_intelligence.complaint_handling_events ("ResponsibleActorId", "ComplaintId", "Revision" DESC);

CREATE INDEX ix_complaint_handling_status ON governance_intelligence.complaint_handling_events ("ToState", "ComplaintId", "Revision" DESC);

CREATE UNIQUE INDEX ux_complaint_handling_one_successor ON governance_intelligence.complaint_handling_events ("PreviousEventId") WHERE "PreviousEventId" IS NOT NULL;

INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261022205550_AddComplaintHandlingEvents', '8.0.2');

COMMIT;


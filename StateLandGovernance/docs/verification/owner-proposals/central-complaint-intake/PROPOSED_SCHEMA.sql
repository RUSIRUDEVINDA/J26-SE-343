-- REVIEW PROPOSAL ONLY. Not an EF migration. NOT APPLIED.
-- Requires explicit schema/authorization/assignment-semantics approval.
-- No existing complaint/account/case/classification row is modified or backfilled.
CREATE TABLE governance_intelligence.complaint_assignment_events (
    "Id" uuid NOT NULL,
    "ComplaintId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "TargetActorId" uuid NOT NULL,
    "TargetOfficeCode" varchar(100) NOT NULL,
    "AssignedByActorId" uuid NOT NULL,
    "AssignedAtUtc" timestamptz NOT NULL,
    "Reason" varchar(2000) NOT NULL,
    "SubmissionKey" uuid NOT NULL,
    "InputSha256" varchar(64) NOT NULL,
    "PreviousAssignmentId" uuid,
    "DirectorySnapshotSha256" varchar(64) NOT NULL,
    "AuthorizationGrantId" uuid NOT NULL,
    "AuthorizationSourceReference" varchar(500) NOT NULL,
    "EventCanonicalJson" text NOT NULL,
    "EventSha256" varchar(64) NOT NULL,
    CONSTRAINT "PK_complaint_assignment_events" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_complaint_assignment_events_Id_ComplaintId" UNIQUE ("Id","ComplaintId"),
    CONSTRAINT "UQ_complaint_assignment_revision" UNIQUE ("ComplaintId","Revision"),
    CONSTRAINT "UQ_complaint_assignment_submission" UNIQUE ("AssignedByActorId","SubmissionKey"),
    CONSTRAINT "FK_complaint_assignment_complaint" FOREIGN KEY ("ComplaintId")
        REFERENCES governance_intelligence.registered_complaints("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_complaint_assignment_previous" FOREIGN KEY ("PreviousAssignmentId","ComplaintId")
        REFERENCES governance_intelligence.complaint_assignment_events("Id","ComplaintId") ON DELETE RESTRICT,
    CONSTRAINT "CK_complaint_assignment_revision" CHECK ("Revision">0),
    CONSTRAINT "CK_complaint_assignment_chain" CHECK (("Revision"=1 AND "PreviousAssignmentId" IS NULL) OR ("Revision">1 AND "PreviousAssignmentId" IS NOT NULL)),
    CONSTRAINT "CK_complaint_assignment_reason" CHECK (length(btrim("Reason"))>0),
    CONSTRAINT "CK_complaint_assignment_office" CHECK (length(btrim("TargetOfficeCode"))>0),
    CONSTRAINT "CK_complaint_assignment_authority" CHECK (length(btrim("AuthorizationSourceReference"))>0),
    CONSTRAINT "CK_complaint_assignment_hashes" CHECK (
        "InputSha256" ~ '^[0-9a-f]{64}$' AND "DirectorySnapshotSha256" ~ '^[0-9a-f]{64}$' AND "EventSha256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "CK_complaint_assignment_canonical" CHECK (length(btrim("EventCanonicalJson"))>0)
);
CREATE UNIQUE INDEX ux_complaint_assignment_one_successor
    ON governance_intelligence.complaint_assignment_events("PreviousAssignmentId")
    WHERE "PreviousAssignmentId" IS NOT NULL;
CREATE INDEX ix_complaint_assignment_target
    ON governance_intelligence.complaint_assignment_events("TargetActorId","ComplaintId","Revision" DESC);
-- Proposed EF migration name: AddCentralComplaintAssignments.
-- Real timestamped migration ID/forward script will be generated only after approval.
-- Down must refuse destructive history removal.

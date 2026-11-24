-- REVIEW ONLY. NOT AN EF MIGRATION. NOT EXECUTED OR APPROVED.
-- No connection, migration-history row, existing-row rewrite, backfill, trigger or extension.
-- Target for any FUTURE separately approved deployment: state_land_governance_c4 / OID34144.

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
    CONSTRAINT fk_investigation_attachment_owner FOREIGN KEY ("InvestigationId", "CreatorActorId")
        REFERENCES governance_intelligence.officer_investigations ("Id", "CreatorActorId") ON DELETE RESTRICT,
    CONSTRAINT ck_investigation_attachment_identity CHECK (
        "Id" <> '00000000-0000-0000-0000-000000000000'::uuid AND
        "CreatorActorId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
        "SubmissionKey" <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT ck_investigation_attachment_concern CHECK ("Concern" IN (
        'LegalDispute','UnauthorizedOccupation','UnauthorizedConstruction',
        'FamilyOrInheritanceClaim','MultipleClaimants','UnresolvedObjection','PreviousIllegalLandActivity')),
    CONSTRAINT ck_investigation_attachment_filename CHECK (
        length(btrim("FileName")) BETWEEN 1 AND 150 AND
        position('/' in "FileName")=0 AND position(chr(92) in "FileName")=0 AND
        position(':' in "FileName")=0 AND "FileName" !~ '[[:cntrl:]]'),
    CONSTRAINT ck_investigation_attachment_type CHECK (
        "ContentType" IN ('application/pdf','image/png','image/jpeg','text/plain')),
    CONSTRAINT ck_investigation_attachment_bytes CHECK (octet_length("Content") BETWEEN 1 AND 5242880),
    CONSTRAINT ck_investigation_attachment_hashes CHECK (
        "ContentSha256" ~ '^[0-9a-f]{64}$' AND "InputSha256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_investigation_attachment_content_hash CHECK (
        encode(sha256("Content"),'hex')="ContentSha256")
);
CREATE INDEX ix_investigation_attachment_history
    ON governance_intelligence.officer_investigation_attachments ("InvestigationId", "CreatedAtUtc", "Id");

CREATE TABLE governance_intelligence.assessment_journey_events (
    "Id" uuid NOT NULL,
    "CreatorActorId" uuid NOT NULL,
    "ProposalId" character varying(120) NOT NULL,
    "Revision" integer NOT NULL,
    "PreviousRevision" integer GENERATED ALWAYS AS ("Revision" - 1) STORED,
    "PreviousEventId" uuid NULL,
    "SubmissionKey" uuid NOT NULL,
    "Operation" character varying(40) NOT NULL,
    "Stage" character varying(16) NOT NULL,
    "State" character varying(16) NOT NULL,
    "InvestigationId" uuid NULL,
    "EarlyRequestId" uuid NULL,
    "RegulatoryEvaluationId" uuid NULL,
    "ConflictProvenanceId" uuid NULL,
    "AnomalyRequestId" uuid NULL,
    "ReservationEventId" uuid NULL,
    "SourceVersion" character varying(100) NOT NULL,
    "InputSha256" character varying(64) NOT NULL,
    "DetailsJson" jsonb NOT NULL,
    "CreatedAtUtc" timestamp(6) with time zone NOT NULL,
    "CanonicalJson" text NOT NULL,
    "EventSha256" character varying(64) NOT NULL,
    CONSTRAINT pk_assessment_journey_events PRIMARY KEY ("Id"),
    CONSTRAINT uq_journey_event_scope UNIQUE ("Id", "CreatorActorId", "ProposalId"),
    CONSTRAINT uq_journey_event_scope_revision UNIQUE ("Id", "CreatorActorId", "ProposalId", "Revision"),
    CONSTRAINT uq_journey_scope_revision UNIQUE ("CreatorActorId", "ProposalId", "Revision"),
    CONSTRAINT uq_journey_submission UNIQUE ("CreatorActorId", "SubmissionKey"),
    CONSTRAINT fk_journey_previous FOREIGN KEY
        ("PreviousEventId", "CreatorActorId", "ProposalId", "PreviousRevision")
        REFERENCES governance_intelligence.assessment_journey_events
        ("Id", "CreatorActorId", "ProposalId", "Revision") ON DELETE RESTRICT,
    CONSTRAINT fk_journey_reservation FOREIGN KEY ("ReservationEventId", "CreatorActorId", "ProposalId")
        REFERENCES governance_intelligence.assessment_journey_events
        ("Id", "CreatorActorId", "ProposalId") ON DELETE RESTRICT,
    CONSTRAINT fk_journey_investigation_owner FOREIGN KEY ("InvestigationId", "CreatorActorId")
        REFERENCES governance_intelligence.officer_investigations ("Id", "CreatorActorId") ON DELETE RESTRICT,
    CONSTRAINT fk_journey_early_owner FOREIGN KEY ("EarlyRequestId", "CreatorActorId")
        REFERENCES governance_intelligence.officer_assessment_requests ("Id", "CreatorActorId") ON DELETE RESTRICT,
    CONSTRAINT fk_journey_regulatory FOREIGN KEY ("RegulatoryEvaluationId")
        REFERENCES governance_intelligence.regulatory_evaluation_provenance ("EvaluationId") ON DELETE RESTRICT,
    CONSTRAINT fk_journey_conflict FOREIGN KEY ("ConflictProvenanceId")
        REFERENCES governance_intelligence.kiwula_assessment_provenance ("Id") ON DELETE RESTRICT,
    CONSTRAINT fk_journey_anomaly_owner FOREIGN KEY ("AnomalyRequestId", "CreatorActorId")
        REFERENCES governance_intelligence.officer_assessment_requests ("Id", "CreatorActorId") ON DELETE RESTRICT,
    CONSTRAINT ck_journey_identity CHECK (
        "Id" <> '00000000-0000-0000-0000-000000000000'::uuid AND
        "CreatorActorId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
        "SubmissionKey" <> '00000000-0000-0000-0000-000000000000'::uuid AND
        length(btrim("ProposalId"))>0 AND length(btrim("SourceVersion"))>0),
    CONSTRAINT ck_journey_revision CHECK (
        ("Revision"=1 AND "PreviousEventId" IS NULL AND "Operation"='JourneyStarted') OR
        ("Revision">1 AND "PreviousEventId" IS NOT NULL AND "Operation"<>'JourneyStarted')),
    CONSTRAINT ck_journey_stage CHECK ("Stage" IN ('Early','Regulatory','Conflict','Anomaly')),
    CONSTRAINT ck_journey_operation CHECK ("Operation" IN (
        'JourneyStarted','InvestigationSelected','InvestigationFinalized','EarlyReserved','EarlySaved',
        'RegulatoryReserved','RegulatorySaved','RegulatoryCompleted',
        'ConflictReserved','ConflictSaved','ConflictCompleted','AnomalyReserved','AnomalySaved',
        'StageRestarted','OutcomeUnknown','ManualReviewHeld')),
    CONSTRAINT ck_journey_operation_stage CHECK (
        ("Operation" IN ('JourneyStarted','InvestigationSelected','InvestigationFinalized',
            'EarlyReserved','EarlySaved','ManualReviewHeld') AND "Stage"='Early') OR
        ("Operation" IN ('RegulatoryReserved','RegulatorySaved','RegulatoryCompleted') AND "Stage"='Regulatory') OR
        ("Operation" IN ('ConflictReserved','ConflictSaved','ConflictCompleted') AND "Stage"='Conflict') OR
        ("Operation" IN ('AnomalyReserved','AnomalySaved') AND "Stage"='Anomaly') OR
        "Operation" IN ('StageRestarted','OutcomeUnknown')),
    CONSTRAINT ck_journey_operation_state CHECK (
        ("Operation" IN ('EarlySaved','RegulatoryCompleted','ConflictCompleted','AnomalySaved') AND "State"='Completed') OR
        ("Operation" IN ('JourneyStarted','StageRestarted') AND "State"='NotStarted') OR
        ("Operation"='OutcomeUnknown' AND "State"='Unknown') OR
        ("Operation" IN ('InvestigationSelected','InvestigationFinalized','EarlyReserved',
            'RegulatoryReserved','RegulatorySaved','ConflictReserved','ConflictSaved',
            'AnomalyReserved','ManualReviewHeld') AND "State"='InProgress')),
    CONSTRAINT ck_journey_reservation_link CHECK (
        ("Operation" IN ('EarlySaved','RegulatorySaved','ConflictSaved','AnomalySaved','OutcomeUnknown')
            AND "ReservationEventId" IS NOT NULL AND "ReservationEventId"<>"Id") OR
        ("Operation" NOT IN ('EarlySaved','RegulatorySaved','ConflictSaved','AnomalySaved','OutcomeUnknown')
            AND "ReservationEventId" IS NULL)),
    CONSTRAINT ck_journey_ancestor_bindings CHECK (
        ("Stage"='Early' AND "RegulatoryEvaluationId" IS NULL AND "ConflictProvenanceId" IS NULL AND "AnomalyRequestId" IS NULL) OR
        ("Stage"='Regulatory' AND "InvestigationId" IS NOT NULL AND "EarlyRequestId" IS NOT NULL AND "ConflictProvenanceId" IS NULL AND "AnomalyRequestId" IS NULL) OR
        ("Stage"='Conflict' AND "InvestigationId" IS NOT NULL AND "EarlyRequestId" IS NOT NULL AND "RegulatoryEvaluationId" IS NOT NULL AND "AnomalyRequestId" IS NULL) OR
        ("Stage"='Anomaly' AND "InvestigationId" IS NOT NULL AND "EarlyRequestId" IS NOT NULL AND "RegulatoryEvaluationId" IS NOT NULL AND "ConflictProvenanceId" IS NOT NULL)),
    CONSTRAINT ck_journey_operation_bindings CHECK (
        ("Operation"='JourneyStarted' AND "InvestigationId" IS NULL AND "EarlyRequestId" IS NULL) OR
        ("Operation" IN ('InvestigationSelected','InvestigationFinalized') AND "InvestigationId" IS NOT NULL AND "EarlyRequestId" IS NULL) OR
        ("Operation" IN ('EarlyReserved','EarlySaved','ManualReviewHeld') AND "InvestigationId" IS NOT NULL AND "EarlyRequestId" IS NOT NULL) OR
        ("Operation" IN ('RegulatorySaved','RegulatoryCompleted') AND "RegulatoryEvaluationId" IS NOT NULL) OR
        ("Operation" IN ('ConflictSaved','ConflictCompleted') AND "ConflictProvenanceId" IS NOT NULL) OR
        ("Operation" IN ('AnomalyReserved','AnomalySaved') AND "AnomalyRequestId" IS NOT NULL) OR
        ("Operation"='RegulatoryReserved' AND "RegulatoryEvaluationId" IS NULL) OR
        ("Operation"='ConflictReserved' AND "ConflictProvenanceId" IS NULL) OR
        ("Operation"='StageRestarted' AND (
            ("Stage"='Early' AND "InvestigationId" IS NULL AND "EarlyRequestId" IS NULL) OR
            ("Stage"='Regulatory' AND "RegulatoryEvaluationId" IS NULL) OR
            ("Stage"='Conflict' AND "ConflictProvenanceId" IS NULL) OR
            ("Stage"='Anomaly' AND "AnomalyRequestId" IS NULL))) OR
        "Operation"='OutcomeUnknown'),
    CONSTRAINT ck_journey_json CHECK (jsonb_typeof("DetailsJson")='object' AND octet_length("DetailsJson"::text)<=65536
        AND octet_length("CanonicalJson") BETWEEN 2 AND 65536 AND jsonb_typeof("CanonicalJson"::jsonb)='object'),
    CONSTRAINT ck_journey_hashes CHECK ("InputSha256" ~ '^[0-9a-f]{64}$' AND "EventSha256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_journey_canonical_hash CHECK (
        encode(sha256(convert_to("CanonicalJson",'UTF8')),'hex')="EventSha256")
);
CREATE UNIQUE INDEX uq_journey_single_successor ON governance_intelligence.assessment_journey_events ("PreviousEventId")
    WHERE "PreviousEventId" IS NOT NULL;
CREATE UNIQUE INDEX uq_journey_reservation_outcome ON governance_intelligence.assessment_journey_events ("ReservationEventId")
    WHERE "ReservationEventId" IS NOT NULL;
CREATE INDEX ix_journey_history ON governance_intelligence.assessment_journey_events
    ("CreatorActorId", "ProposalId", "Revision" DESC);
-- No FK index additions to existing tables; all referenced keys already exist.
-- Cross-row lifecycle policy, saved-state/kind checks and mandatory review checks remain server responsibilities.

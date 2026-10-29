-- REVIEW ONLY. Not an EF migration, executed SQL or deployment authorization.
-- Proposed isolated-only addition to the approved attachment-table scope.
-- Retains every existing allowed operation and every historical row.
ALTER TABLE governance_intelligence.officer_action_events
    DROP CONSTRAINT ck_officer_operation;
ALTER TABLE governance_intelligence.officer_action_events
    ADD CONSTRAINT ck_officer_operation CHECK (
        "Operation" IN ('InvestigationSaved','RequestSubmitted','InferenceStarted',
                        'AssessmentSaved','AssessmentFailed','OutcomeUnknown')
        OR ("Operation" = 'InvestigationFinalized'
            AND "InvestigationId" IS NOT NULL AND "RequestId" IS NULL)
    );

-- Immutable investigations finalize at most once. Corrections create a new
-- investigation snapshot; they do not replace or append contradictory finals.
CREATE UNIQUE INDEX ux_officer_investigation_finalization
    ON governance_intelligence.officer_action_events ("InvestigationId")
    WHERE "Operation" = 'InvestigationFinalized';

-- No additional GRANT: the existing event store already has SELECT/INSERT.
-- If approved, EF generates this delta and the attachment-table delta together;
-- review and apply atomically ONLY to a NEW isolated restored database.

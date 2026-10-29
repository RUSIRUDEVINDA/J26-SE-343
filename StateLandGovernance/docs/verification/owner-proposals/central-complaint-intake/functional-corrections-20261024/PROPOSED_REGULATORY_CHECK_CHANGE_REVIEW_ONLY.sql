-- REVIEW ONLY. NOT EXECUTED. FUTURE operator migration must be atomic.
-- The old CHECK is replaced; no data, index, FK, remarks, status or revision constraint changes.
ALTER TABLE governance_intelligence.regulatory_followup_events
    DROP CONSTRAINT ck_regulatory_followup_operation;
ALTER TABLE governance_intelligence.regulatory_followup_events
    ADD CONSTRAINT ck_regulatory_followup_operation CHECK (
        ("Operation"='ClarificationRequested' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NOT NULL
            AND length("MissingInformation") BETWEEN 20 AND 2000)
        OR
        ("Operation"='HumanReviewRecorded' AND "MissingInformation" IS NULL AND "ReviewOutcome" IS NOT NULL
            AND "ReviewOutcome" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview'))
        OR
        ("Operation"='NoFollowupRequired' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NULL)
    );
-- Existing ck_regulatory_followup_remarks still requires 20..2000 characters for ALL operations.
-- Existing per-assessment/rule revision uniqueness, predecessor FK, canonical hashes and SELECT/INSERT rights stay unchanged.
-- This CHECK validates shape only; it CANNOT waive a mandatory saved human-review/legal requirement.

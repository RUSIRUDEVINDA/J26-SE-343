START TRANSACTION;

ALTER TABLE governance_intelligence.regulatory_followup_events DROP CONSTRAINT ck_regulatory_followup_operation;

CREATE UNIQUE INDEX ux_regulatory_finding_closure ON governance_intelligence.regulatory_followup_events ("EvaluationId", "RuleCode") WHERE "Operation"='FindingVerifiedClosed';

ALTER TABLE governance_intelligence.regulatory_followup_events ADD CONSTRAINT ck_regulatory_followup_operation CHECK (("Operation"='ClarificationRequested' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NOT NULL AND length("MissingInformation") BETWEEN 20 AND 2000) OR ("Operation"='HumanReviewRecorded' AND "MissingInformation" IS NULL AND "ReviewOutcome" IS NOT NULL AND "ReviewOutcome" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview')) OR ("Operation"='NoFollowupRequired' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NULL) OR ("Operation"='FindingVerifiedClosed' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NULL AND length("Remarks")>=40 AND jsonb_array_length("ReferencesJson")>=1) );

INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261024112715_AllowIndividualFindingClosure', '8.0.2');

COMMIT;


START TRANSACTION;

ALTER TABLE governance_intelligence.regulatory_followup_events DROP CONSTRAINT ck_regulatory_followup_operation;

ALTER TABLE governance_intelligence.regulatory_followup_events ADD CONSTRAINT ck_regulatory_followup_operation CHECK (("Operation"='ClarificationRequested' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NOT NULL AND length("MissingInformation") BETWEEN 20 AND 2000) OR ("Operation"='HumanReviewRecorded' AND "MissingInformation" IS NULL AND "ReviewOutcome" IS NOT NULL AND "ReviewOutcome" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview')) OR ("Operation"='NoFollowupRequired' AND "ReviewOutcome" IS NULL AND "MissingInformation" IS NULL));

INSERT INTO governance_intelligence.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261024022733_AllowRegulatoryNoFollowupRequired', '8.0.2');

COMMIT;


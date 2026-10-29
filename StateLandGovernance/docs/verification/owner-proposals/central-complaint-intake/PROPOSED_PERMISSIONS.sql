-- REVIEW ONLY. NOT APPLIED. Separate operator connection deploys reviewed EF SQL.
-- Runtime keeps all its existing rights, unchanged.
GRANT SELECT, INSERT ON governance_intelligence.complaint_assignment_events TO slg_c4_regulatory_runtime;
-- No new UPDATE, DELETE, schema CREATE, DDL, role, account/C1/C3/referral/outbox grants.
-- Existing UPDATE(Revision,PayloadJson) remains limited to normal complaint retry/review.

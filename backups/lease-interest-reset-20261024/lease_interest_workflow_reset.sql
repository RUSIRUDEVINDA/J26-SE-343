-- Scoped Component 1 lease-interest workflow reset (executed 2026-10-24)
-- Database: land_intel_colombo_state_research @ localhost:5432
-- Order respects FK: authority_decisions -> valuations (RESTRICT)

BEGIN;

DELETE FROM land_intelligence.lease_interest_authority_decisions;
DELETE FROM land_intelligence.lease_interest_valuations;
DELETE FROM land_intelligence.lease_interest_status_history;
DELETE FROM land_intelligence.lease_interest_requests;

COMMIT;

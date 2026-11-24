-- Review-only: empty auto-created drafts from before explicit Save draft behavior.
-- Run in a read-only transaction first; delete only after manual review.
--
-- Candidates: status = Draft (1), default empty payload markers, never submitted.
-- Adjust filters to match your environment and retention policy.

-- 1) List candidate rows (inspect owner, parcel, timestamps)
SELECT
    r.id,
    r.owner_id,
    r.land_parcel_id,
    r.created_at_utc,
    r.updated_at_utc,
    r.payload_json
FROM land_intelligence.lease_interest_requests r
WHERE r.status = 1
  AND r.submitted_at_utc IS NULL
  AND r.consent_to_preliminary_assessment = false
  AND r.requested_purpose = 0
  AND r.payload_json LIKE '%"legalName":""%'
ORDER BY r.updated_at_utc DESC;

-- 2) Optional delete (commented — run only after sign-off)
-- DELETE FROM land_intelligence.lease_interest_status_history h
-- USING land_intelligence.lease_interest_requests r
-- WHERE h.lease_interest_request_id = r.id
--   AND r.status = 1
--   AND r.submitted_at_utc IS NULL
--   AND r.consent_to_preliminary_assessment = false
--   AND r.requested_purpose = 0
--   AND r.payload_json LIKE '%"legalName":""%';
--
-- DELETE FROM land_intelligence.lease_interest_requests r
-- WHERE r.status = 1
--   AND r.submitted_at_utc IS NULL
--   AND r.consent_to_preliminary_assessment = false
--   AND r.requested_purpose = 0
--   AND r.payload_json LIKE '%"legalName":""%';

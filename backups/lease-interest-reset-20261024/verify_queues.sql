SELECT 'officer_queue' AS check_name, COUNT(*)::bigint AS row_count
FROM land_intelligence.lease_interest_requests
WHERE "Status" IN (3, 4)
UNION ALL
SELECT 'authority_referred', COUNT(*)::bigint
FROM land_intelligence.lease_interest_requests
WHERE "Status" = 6
UNION ALL
SELECT 'approved_in_principle', COUNT(*)::bigint
FROM land_intelligence.lease_interest_requests
WHERE "Status" = 7
UNION ALL
SELECT 'total_requests', COUNT(*)::bigint
FROM land_intelligence.lease_interest_requests;

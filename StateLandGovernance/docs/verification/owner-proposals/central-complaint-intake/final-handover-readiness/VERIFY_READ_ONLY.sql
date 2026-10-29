-- Run in pgAdmin connected ONLY to localhost:5432/state_land_governance_c4.
-- Metadata/results only: no complaint text, evidence bytes, private notes or actor identities.
BEGIN READ ONLY;
SET LOCAL timezone = 'UTC';
SELECT current_database(), inet_server_port(), current_user,
       current_setting('transaction_read_only');
SELECT oid FROM pg_database WHERE datname=current_database();
SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY "MigrationId";

-- Original findings carry the evaluated source/version. A current catalogue is not a substitute.
SELECT e."Id",e."EvaluationTimestamp",e."RuleSetVersion",e."Status",
 f->>'ruleCode' AS rule_code,f->>'ruleVersion' AS rule_version,
 f->>'status' AS finding_status,f->>'observedValueSummary' AS explanation,
 f->>'expectedRequirement' AS recorded_requirement,
 f->>'recommendedAction' AS next_action,f->>'sourceAuthority' AS source_authority,
 f->>'sourceDocument' AS source_document,f->>'sourceSection' AS source_section
FROM governance_intelligence.compliance_evaluations e
CROSS JOIN LATERAL jsonb_array_elements(COALESCE(e."FindingsJson",'[]'::jsonb)) f
ORDER BY e."EvaluationTimestamp" DESC,e."Id",f->>'ruleCode';

SELECT r."RuleCode",r."RuleVersion",r."RuleType",r."Description",r."SourceSection",
 s."Authority",s."DocumentTitle",s."DocumentVersion"
FROM governance_intelligence.compliance_rules r
LEFT JOIN governance_intelligence.regulatory_sources s ON s."Id"=r."SourceId"
ORDER BY r."RuleCode",r."RuleVersion";

-- Independent submissions preserve each result and input version; no inferred supersedes link.
SELECT e."Id",e."EvaluationTimestamp",e."Status",e."RuleSetVersion",
 p."ProvenanceJson"->>'inputVersion' AS input_version,
 p."ProvenanceJson"->>'evaluatedInputHash' AS input_hash,
 p."ProvenanceJson"->>'catalogueVersion' AS catalogue_version
FROM governance_intelligence.compliance_evaluations e
JOIN governance_intelligence.regulatory_evaluation_provenance p ON p."EvaluationId"=e."Id"
ORDER BY e."EvaluationTimestamp" DESC,e."Id";

SELECT "Id","Status","DetectedConflictsCount","EvaluationTimestamp"
FROM governance_intelligence.conflict_evaluations ORDER BY "EvaluationTimestamp" DESC;
SELECT assessment_id,input_version,created_at_utc,jsonb_object_keys(result_snapshot_json) AS available_result_fields
FROM governance_intelligence.early_governance_screening_evaluations ORDER BY created_at_utc DESC;
SELECT "Id","Kind","State","CreatedAtUtc","FinishedAtUtc","EarlyAssessmentId","AnomalyAssessmentId"
FROM governance_intelligence.officer_assessment_requests ORDER BY "CreatedAtUtc" DESC;

SELECT assessment_id,assessed_at_utc,model_version,model_artifact_sha256,input_sha256,
 anomaly_score,threshold,flagged,event_count,elapsed_days,max_gap_hours,mean_gap_hours,
 unique_activities,repeated_activity_count,resource_handoffs,institution_switches,distinct_resources
FROM governance_intelligence.workflow_anomaly_assessments ORDER BY assessed_at_utc DESC;

SELECT assessment_id,assessed_at_utc,model_version,predicted_category,
 administrative_procedural_integrity_probability,lease_revenue_payment_enforcement_probability,
 unauthorized_allocation_transfer_use_probability,protected_environmental_lease_misuse_probability
FROM governance_intelligence.complaint_classification_assessments ORDER BY assessed_at_utc DESC;

SELECT "Id","RequestId","Concern","ContentType",octet_length("Content") AS bytes,
 "ContentSha256","CreatedAtUtc" FROM governance_intelligence.screening_evidence_attachments;
SELECT "Id","RequestId","Concern","Revision","PreviousStatus","Status","CreatedAtUtc","EventSha256"
FROM governance_intelligence.screening_evidence_verifications ORDER BY "RequestId","Concern","Revision";
SELECT "Id","ComplaintId","Revision","PreviousAssignmentId","AssignedAtUtc","EventSha256"
FROM governance_intelligence.complaint_assignment_events ORDER BY "ComplaintId","Revision";
SELECT "Id","ComplaintId","Revision","Operation","FromState","ToState","HandlingMode","ActedAtUtc","EventSha256"
FROM governance_intelligence.complaint_handling_events ORDER BY "ComplaintId","Revision";

-- New follow-up persistence is NOT deployed. Do not query invented columns/tables as existing.
SELECT to_regclass('governance_intelligence.regulatory_followup_events') AS proposed_followup_table;
COMMIT;

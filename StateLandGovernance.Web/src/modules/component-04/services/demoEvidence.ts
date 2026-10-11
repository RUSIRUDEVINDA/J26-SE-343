// Presentation-only fictional fixtures. These are not published API contracts.
export const complaintCategories = [
  "Administrative / Procedural / Integrity",
  "Lease Revenue / Payment / Enforcement",
  "Unauthorized Allocation / Transfer / Use",
  "Protected / Environmental Lease Misuse",
] as const;

// Exact EarlyGovernanceIndicatorType names; display labels preserve their meaning.
export const screeningIndicators = [
  ["LegalDispute", "Legal dispute"],
  ["UnauthorizedOccupation", "Unauthorized occupation"],
  ["UnauthorizedConstruction", "Unauthorized construction"],
  ["FamilyOrInheritanceClaim", "Family or inheritance claim"],
  ["MultipleClaimants", "Multiple claimants"],
  ["UnresolvedObjection", "Unresolved objection"],
  ["PreviousIllegalLandActivity", "Previous illegal land activity"],
] as const;

export const demoComplaints = [
  { assessmentId: "DEMO-COMPLAINT-002", caseId: "DEMO-C4-005", complaint: "Fictional example: an applicant reports that a structure was built on the demonstration parcel without permission.", predictedCategory: complaintCategories[2], probabilities: [.12, .08, .74, .06], modelVersion: "FICTIONAL-CLASSIFIER-v1", assessedAt: "2026-10-01T09:15:00Z" },
  { assessmentId: "DEMO-COMPLAINT-001", caseId: "DEMO-C4-005", complaint: "Fictional earlier report: the applicant has not received a response to their request for a status update.", predictedCategory: complaintCategories[0], probabilities: [.72, .1, .12, .06], modelVersion: "FICTIONAL-CLASSIFIER-v1", assessedAt: "2026-09-28T08:00:00Z" },
] as const;

export const demoAnomaly = {
  assessmentId: "DEMO-ANOMALY-001", caseId: "DEMO-TRACE-001", workflowRunId: "DEMO-RUN-001",
  evidenceVersion: "DEMO-COMPLETED-TRACE-v1", modelVersion: "FICTIONAL-ISOLATION-FOREST-v1",
  assessedAt: "2026-10-01T09:00:00Z", anomalyScore: 0.14, threshold: 0.06, flagged: true,
  features: { event_count: 12, elapsed_days: 94, max_gap_hours: 720, mean_gap_hours: 205.09, unique_activities: 8, repeated_activity_count: 4, resource_handoffs: 7, institution_switches: 5, distinct_resources: 6 },
};

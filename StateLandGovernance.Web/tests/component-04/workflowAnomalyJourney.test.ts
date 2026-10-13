import { describe, it } from "node:test";
import assert from "node:assert";
import fs from "node:fs";
import React from "react";
import { renderToString } from "react-dom/server";
import { WorkflowAnomalyJourneyView } from "@/modules/component-04/components/WorkflowAnomalyJourney";
import type { PersistedWorkflowAnomalyAssessment, WorkflowProjectionJourney } from "@/modules/component-04/services/governanceService";

const projection: WorkflowProjectionJourney = {
  projectionId: "7bdcb5d7-79e0-53ec-933e-df6c898ec3a3",
  caseId: "PP1-ANOMALY-COMMISSIONER-001",
  workflowRunId: "b1694cb5-60a5-5817-b11b-55bead079189",
  workflowState: "Completed",
  traceCompleteness: "CompletedTraceEligibleForAnomalyScoring",
  assessmentEligibility: "Eligible",
  legalRouteLabel: { availability: "Unavailable", value: null },
  isSynthetic: true,
  provenanceKind: "SyntheticPp1Seed",
  purpose: "PP1Demonstration",
  syntheticSeedId: "PP1-ANOMALY-COMMISSIONER-001",
  syntheticSeedVersion: "pp1-anomaly-commissioner-v1",
  events: [
    { eventSequence: 1, activity: "Complete Proposal Received", institution: "DS", resource: "DS-Intake", actorRole: "IntakeOfficer", timestamp: "2026-08-03T10:00:00", status: "Completed", evidenceReference: "seed:1" },
    { eventSequence: 2, activity: "Land Registry Registration", institution: "LAND_REGISTRY", resource: "Route-Control", actorRole: "RouteController", timestamp: "2026-08-03T10:00:00", status: "NotApplicable", evidenceReference: "seed:2" },
    { eventSequence: 3, activity: "Correction Requested", institution: "LCGD", resource: "Reviewer", actorRole: "ReviewOfficer", timestamp: "2026-08-04T10:00:00", status: "Completed", evidenceReference: "seed:3" },
    { eventSequence: 4, activity: "DS Final Notification", institution: "DS", resource: "DS-Officer", actorRole: "LandOfficer", timestamp: "2026-08-05T10:00:00", status: "Completed", evidenceReference: "seed:4" },
  ],
  routeApplicability: {
    requiredClearances: { availability: "Unavailable", value: null },
    higherApprovalApplicable: { availability: "Unavailable", value: null },
    registrationApplicable: { availability: "Unavailable", value: null },
  },
  completeProposalBoundary: { availability: "Confirmed", occurredAt: "2026-08-03T10:00:00Z", source: "SyntheticPp1Seed", sourceReference: "complete" },
  applicantHandoverBoundary: { availability: "Confirmed", occurredAt: "2026-08-05T10:00:00Z", source: "SyntheticPp1Seed", sourceReference: "handover" },
  ingestedAtUtc: "2026-10-12T10:00:00Z",
};

const assessment: PersistedWorkflowAnomalyAssessment = {
  assessmentId: "11111111-1111-1111-1111-111111111111",
  workflowRunId: projection.workflowRunId,
  caseId: projection.caseId,
  workflowProjectionId: projection.projectionId,
  modelVersion: "workflow_anomaly_model_v1",
  anomalyScore: 0.61,
  threshold: 0.5743688923115832,
  flagged: true,
  eventCount: 4,
  elapsedDays: 2,
  maxGapHours: 24,
  meanGapHours: 16,
  uniqueActivities: 4,
  repeatedActivityCount: 0,
  resourceHandoffs: 3,
  institutionSwitches: 2,
  distinctResources: 4,
  advisoryNote: "Advisory only.",
  assessedAtUtc: "2026-10-12T10:05:00Z",
};

const render = (value: PersistedWorkflowAnomalyAssessment | null = assessment, source = projection) =>
  renderToString(React.createElement(WorkflowAnomalyJourneyView, { projection: source, assessment: value }));

describe("Commissioner workflow anomaly journey", () => {
  it("renders persisted events in sequence with conditional and correction markers", () => {
    const html = render();
    const first = html.indexOf("Complete Proposal Received");
    const conditional = html.indexOf("Land Registry Registration");
    const correction = html.indexOf("Correction Requested");
    const last = html.indexOf("DS Final Notification");
    assert.ok(first < conditional && conditional < correction && correction < last);
    assert.match(html, /Not applicable/);
    assert.match(html, /Correction \/ repeated-stage marker/);
    assert.match(html, /<ol/);
    assert.doesNotMatch(html, /Route evidence and availability/);
  });

  it("renders synthetic provenance, confirmed processing stages, and all nine features", () => {
    const html = render();
    assert.match(html, /SyntheticPp1Seed/);
    assert.match(html, /Synthetic upstream evidence/);
    assert.match(html, /Assessment persisted/);
    for (const label of ["Event count", "Elapsed days", "Maximum gap hours", "Mean gap hours", "Unique activities", "Repeated activity count", "Resource handoffs", "Institution switches", "Distinct resources"]) {
      assert.ok(html.includes(label), `missing ${label}`);
    }
  });

  it("renders flagged and unflagged guidance without probability or confidence labels", () => {
    const flagged = render();
    const unflagged = render({ ...assessment, flagged: false, anomalyScore: 0.5 });
    assert.match(flagged, /Flagged for review/);
    assert.match(flagged, /Review the workflow history and supporting records/);
    assert.match(unflagged, /No statistical escalation/);
    assert.match(unflagged, /does not prove that the workflow is compliant/);
    assert.doesNotMatch(flagged, /anomaly probability|anomaly confidence/i);
    assert.match(flagged, /not a probability or confidence value/);
  });

  it("renders missing and partial evidence truthfully", () => {
    const partial = render(null, { ...projection, workflowState: "Ongoing", assessmentEligibility: "Ineligible", traceCompleteness: "PartialTraceIneligibleForAnomalyScoring" });
    assert.match(partial, /Persisted anomaly assessment not found/);
    assert.match(partial, /Officer review has not been performed/);
    assert.doesNotMatch(partial, /Run and persist frozen-model assessment/);
    assert.doesNotMatch(partial, /Route evidence and availability/);
  });

  it("allows an eligible persisted trace to run a fresh model inference", () => {
    const html = renderToString(React.createElement(WorkflowAnomalyJourneyView, {
      projection,
      assessment,
      onAssess: () => undefined,
    }));

    assert.match(html, /Run the workflow anomaly model again/);
    assert.match(html, /Run and persist a new frozen-model assessment/);
    assert.ok(html.indexOf("Synthetic C1/C3 handoff evidence") < html.indexOf("Run the workflow anomaly model again"));
    assert.ok(html.indexOf("Run the workflow anomaly model again") < html.indexOf("Institutional workflow timeline"));
  });

  it("shows live sequential trace progress while model inference is running", () => {
    const html = renderToString(React.createElement(WorkflowAnomalyJourneyView, {
      projection,
      assessment,
      processing: true,
      processedEventCount: 1,
      busy: true,
    }));

    const textWithoutHydrationMarkers = html.replace(/<!--.*?-->/g, "");
    assert.match(textWithoutHydrationMarkers, /Applying the workflow anomaly model to trace event 2 of 4/);
    assert.match(html, /aria-label="Event 1 processed"/);
    assert.match(html, /aria-label="Analyzing event 2 of 4"/);
    assert.match(html, /aria-label="Event 3 waiting"/);
  });

  it("contains no client threshold calculation, raw JSON path, mock fallback, or audit verification call", () => {
    const componentSource = fs.readFileSync("src/modules/component-04/components/WorkflowAnomalyJourney.tsx", "utf8");
    assert.doesNotMatch(componentSource, /flagged\s*=|anomalyScore\s*>|\.anomalyScore\s*>/);
    assert.doesNotMatch(componentSource, /JSON\.parse|textarea|Workflow Trace JSON/);
    assert.doesNotMatch(componentSource, /mock|demoEvidence|getAuditReceipt|verifyAuditIntegrity/i);
    assert.doesNotMatch(componentSource, /Route evidence and availability|Retrieve case journey|allowCaseLookup/);
  });
});

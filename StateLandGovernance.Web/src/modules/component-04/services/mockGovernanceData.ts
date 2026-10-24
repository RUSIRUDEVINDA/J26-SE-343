import type { GovernanceDashboardData } from "../types/governance";

export const MOCK_GOVERNANCE_DASHBOARD_DATA: GovernanceDashboardData = {
  summary: {
    totalCases: 5,
    totalAssessments: 5,
    clearCount: 2,
    reviewRequiredCount: 2,
    insufficientInfoCount: 1,
    lastUpdated: "2026-10-01T09:15:00.000Z",
  },
  cases: [
    {
      assessmentId: "DEMO-ASSESSMENT-005",
      workflowRunId: null,
      caseId: "DEMO-C4-005",
      parcelReference: "WP-COL-0814",
      applicantReference: "Fictional Bioenergy Applicant",
      overallStatus: "ReviewRequired",
      complianceStatus: "RequiresReview",
      conflictStatus: "ConflictDetected",
      earlyScreeningStatus: "ReviewRequired",
      anomalyStatus: "InsufficientHistory",
      consensusOutcome: "Pending",
      evaluatedAt: "2026-10-01T09:15:00.000Z",
      version: "v1.2-snap",
      summaryFinding:
        "Unsupported decision difference — another application is linked to the same parcel while this case remains active. Four institutions approved, but one rejection has no clear rule reference or supporting evidence.",
      rationale:
        "The case was flagged because an unapproved structure was reported on the parcel, but the available decision record does not contain sufficient supporting evidence to confirm the situation. Further administrative verification is required before the case can proceed.",
      recommendedNextStep:
        "Schedule an administrative/site verification by an authorized land officer to confirm the reported structure, review the relevant records and supporting evidence, and update the case findings before further processing.",
      complaintAdvisory: {
        hasComplaint: true,
        predictedCategory: "Unauthorized Allocation / Transfer / Use",
        modelProbability: 0.74,
        reviewStatus: "UnderReview",
        modelVersion: "LR-Baseline-v1",
      },
    },
    {
      assessmentId: "DEMO-ASSESSMENT-001",
      workflowRunId: null,
      caseId: "DEMO-C4-001",
      parcelReference: "CP-KAN-0042",
      applicantReference: "Fictional Tea Applicant",
      overallStatus: "Clear",
      complianceStatus: "Compliant",
      conflictStatus: "Clear",
      earlyScreeningStatus: "Clear",
      anomalyStatus: "NotAssessed",
      consensusOutcome: "ConsensusReached",
      evaluatedAt: "2026-10-01T08:30:00.000Z",
      version: "v1.0-snap",
      summaryFinding:
        "Recorded compliance and conflict checks are clear in this fictional snapshot. Workflow anomaly assessment is not assessed.",
      rationale:
        "Verified records substantiate 30-year agricultural lease duration, environmental zoning clearance from Central Environmental Authority, and unanimous institutional concurrence.",
      recommendedNextStep:
        "Compile governance clearance dossier and submit to the Land Commissioner for formal decision support.",
    },
    {
      assessmentId: "DEMO-ASSESSMENT-002",
      workflowRunId: null,
      caseId: "DEMO-C4-002",
      parcelReference: "SP-GAL-0119",
      applicantReference: "Fictional Logistics Applicant",
      overallStatus: "InsufficientInformation",
      complianceStatus: "InsufficientInformation",
      conflictStatus: "Clear",
      earlyScreeningStatus: "InsufficientInformation",
      anomalyStatus: "InsufficientHistory",
      consensusOutcome: "Pending",
      evaluatedAt: "2026-09-30T16:45:00.000Z",
      version: "v1.1-snap",
      summaryFinding:
        "Pending prerequisite coastal buffer zone appraisal and verified clearance documentation.",
      rationale:
        "Coast Conservation and Coastal Resource Management Department statutory endorsement is pending submission. Early screening indicator marked as unverified.",
      recommendedNextStep:
        "Issue formal administrative reminder requesting authenticated statutory coastal conservation clearance before continuing governance assessment.",
    },
    {
      assessmentId: "DEMO-ASSESSMENT-003",
      workflowRunId: null,
      caseId: "DEMO-C4-003",
      parcelReference: "EP-AMP-0512",
      applicantReference: "Fictional Tourism Applicant",
      overallStatus: "ReviewRequired",
      complianceStatus: "Conditional",
      conflictStatus: "ConflictDetected",
      earlyScreeningStatus: "ReviewRequired",
      anomalyStatus: "UnderReview",
      consensusOutcome: "ConditionalConsensus",
      evaluatedAt: "2026-09-30T14:20:00.000Z",
      version: "v1.0-snap",
      summaryFinding:
        "Spatial parcel boundary overlap detected with pre-existing tourism conservation reservation.",
      rationale:
        "Geospatial parcel boundary coordinates intersect by 12.4% with protected state reservation parcel EP-AMP-RES-004. Preliminary complaint registered regarding lease boundary alignment.",
      recommendedNextStep:
        "Conduct joint cadastral boundary verification between Divisional Secretariat and SLTDA land surveyor prior to final assessment.",
      complaintAdvisory: {
        hasComplaint: true,
        predictedCategory: "Protected / Environmental Lease Misuse",
        modelProbability: 0.69,
        reviewStatus: "FlaggedForSiteInspection",
        modelVersion: "LR-Baseline-v1",
      },
    },
    {
      assessmentId: "DEMO-ASSESSMENT-004",
      workflowRunId: null,
      caseId: "DEMO-C4-004",
      parcelReference: "NW-KUR-0305",
      applicantReference: "Fictional Agriculture Applicant",
      overallStatus: "Clear",
      complianceStatus: "Compliant",
      conflictStatus: "Clear",
      earlyScreeningStatus: "Clear",
      anomalyStatus: "NotAssessed",
      consensusOutcome: "ConsensusReached",
      evaluatedAt: "2026-09-29T11:00:00.000Z",
      version: "v1.0-snap",
      summaryFinding:
        "Comprehensive regulatory compliance verified across zoning, tenure, and financial appraisal metrics.",
      rationale:
        "Standard agro-processing lease application with valid lease value ratio, appropriate NPV appraisal, and verified absence of jurisdictional encumbrances.",
      recommendedNextStep:
        "Review the remaining unassessed checks before drawing an overall conclusion.",
    },
  ],
};

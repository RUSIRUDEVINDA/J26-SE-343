export type ProposalPresetKey = "proposal-1" | "proposal-2" | "proposal-3";

export interface ProposalPresetMeta {
  key: ProposalPresetKey;
  shortLabel: string;
  title: string;
  caseId: string;
  anomalyCaseId: string;
  summary: string;
  auditRecordId: string;
}

export const PROPOSAL_PRESETS: Record<ProposalPresetKey, ProposalPresetMeta> = {
  "proposal-1": {
    key: "proposal-1",
    shortLabel: "Proposal 1 · Clean Lease (PP1-FINAL-CLEAR-001)",
    title: "Proposal 1 — Standard Clean Lease (No Escalation)",
    caseId: "PP1-FINAL-CLEAR-001",
    anomalyCaseId: "PP1-ANOMALY-COMMISSIONER-001",
    summary: "Legacy synthetic proposal input preset. Outcomes, anomaly scores and audit status must be retrieved from actual backend evaluations; none is promised by this selection.",
    auditRecordId: "083d4e8b-6a13-4791-8d65-e05b4f3515ea",
  },
  "proposal-2": {
    key: "proposal-2",
    shortLabel: "Proposal 2 · Early Referral (PP1-FINAL-REFERRAL-001)",
    title: "Proposal 2 — Boundary / Legal Dispute Referred Early",
    caseId: "PP1-FINAL-REFERRAL-001",
    anomalyCaseId: "PP1-FINAL-DELAYED-001",
    summary: "Legacy synthetic boundary-dispute input preset. Supplied concerns require review; referral or assessment status is known only from the backend.",
    auditRecordId: "083d4e8b-6a13-4791-8d65-e05b4f3515ea",
  },
  "proposal-3": {
    key: "proposal-3",
    shortLabel: "Proposal 3 · Full Escalation (PP1-FINAL-COMBINED-001)",
    title: "Proposal 3 — Complex Escalated Lease (Full Component 4 Stack)",
    caseId: "PP1-FINAL-COMBINED-001",
    anomalyCaseId: "PP1-FINAL-COMBINED-001",
    summary: "Legacy synthetic concern/complaint input preset. This description supplies no classifier probability, anomaly score, ledger transaction or clearance result.",
    auditRecordId: "083d4e8b-6a13-4791-8d65-e05b4f3515ea",
  },
};

export const PROPOSAL_PRESET_LIST: ProposalPresetMeta[] = [
  PROPOSAL_PRESETS["proposal-1"],
  PROPOSAL_PRESETS["proposal-2"],
  PROPOSAL_PRESETS["proposal-3"],
];

function createCompleteComplianceInput(proposalId: string) {
  return {
    proposalId,
    location: {
      province: "Southern",
      district: "Hambantota",
      divisionalSecretariatDivision: "Hambantota",
      gramaNiladhariDivision: "Hambantota West",
      description: "State-land lease proposal demonstration parcel.",
    },
    landRequirement: {
      requiresLand: true,
      extentHectares: 2.5,
      landType: "Commercial",
      allocationDetails: "Lease allocation supported by the referenced survey plan.",
      resettlementApplicable: false,
    },
    preliminaryAssessment: {
      preFeasibilityDone: true,
      preFeasibilityRef: "PRE-FEAS-2026-001",
      eiaDone: true,
      eiaRef: "CEA-EIA-2026-001",
      assessmentReportReferences: ["ASSESSMENT-2026-001"],
    },
    policyAlignment: {
      declaredPolicies: ["National Physical Planning Policy"],
      alignmentExplanation: "The proposed use follows the declared regional development policy.",
    },
    stakeholders: {
      expectedStakeholders: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
      consultedStakeholders: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
      duplicationAssessmentSupplied: true,
      overlapMitigationRef: "OVERLAP-2026-001",
    },
    resultFramework: {
      nodes: [{ nodeId: "OUT-1", nodeType: "Impact", title: "Lease site operational", parentNodeId: null as string | null, kpiReferences: ["KPI-1"] }],
    },
    impactAssessment: { impactAssessmentApplicable: false, negativeImpacts: [] },
    riskFramework: {
      risks: [{ riskId: "RISK-1", description: "Construction delay", mitigationStrategy: "Milestone monitoring", isAssumption: false }],
    },
    disasterRiskAssessment: {
      applicable: false,
      assessmentCompleted: false,
      assessmentReference: null,
      hazardsConsidered: [],
      mitigationMeasures: [],
      responsibleRole: "District Planning Officer",
      remarks: "No material disaster-risk exposure identified during screening.",
    },
    monitoringPlan: {
      kpis: [{
        kpiId: "KPI-1",
        outputOrOutcomeRef: "OUT-1",
        unitOfMeasure: "Percent complete",
        baselineValue: 0,
        baselineYear: 2026,
        targetValue: 100,
        meansOfVerification: "Site inspection",
        dataSource: "District monitoring register",
        responsibleRole: "Land Officer",
      }],
    },
    budget: {
      submittedProjectBudget: 100000,
      costComponents: [{ componentId: "COST-1", name: "Capital works", amount: 100000 }],
    },
    financing: {
      financingSources: [{ sourceId: "FUND-1", name: "Applicant equity", amount: 100000 }],
      revenueExpected: false,
      revenueForecastAmount: null,
    },
    socialSafeguard: {
      resettlementApplicable: false,
      resettlementCost: null,
      genderConsidered: true,
      accessibilityConsidered: true,
    },
    implementation: {
      activities: [{ activityId: "ACT-1", name: "Site development", responsibleRole: "Project Manager" }],
      oAndMArrangement: "Annual preventive-maintenance plan",
      oAndMCost: 5000,
      oAndMFundingSource: "Operating revenue",
    },
    economicAppraisal: {
      selectedMethod: "NPV",
      discountRate: 0.1,
      cashFlows: [-100000, 30000, 40000, 50000],
      cashFlowPeriod: "Annual",
      submittedNpv: null,
      submittedIrr: null,
      submittedPaybackPeriod: null,
      submittedCbr: null,
      submittedPvBenefits: null,
      submittedPvCosts: null,
      cbrConvention: "BenefitsOverCosts",
    },
    evidenceReferences: [{ evidenceId: "EVID-1", referenceNumber: "REF-2026-001", verificationStatus: "Verified" }],
  };
}

export function getComplianceEvaluationPreset(presetKey: ProposalPresetKey): {
  actionName: string;
  caseId: string;
  inputJson: string;
} {
  const meta = PROPOSAL_PRESETS[presetKey];
  const input = createCompleteComplianceInput(meta.caseId);

  if (presetKey === "proposal-2") {
    input.location.gramaNiladhariDivision = "";
    input.landRequirement.allocationDetails = "";
    input.preliminaryAssessment.eiaRef = "";
    input.stakeholders.consultedStakeholders = ["DS-HAMBANTOTA", "CEA"];
    input.economicAppraisal.selectedMethod = "";
  } else if (presetKey === "proposal-3") {
    input.landRequirement.requiresLand = false;
    input.landRequirement.extentHectares = 7.5;
    input.budget.submittedProjectBudget = 200000;
    input.financing.financingSources[0].amount = 60000;
    input.financing.revenueExpected = true;
    input.financing.revenueForecastAmount = null;
    input.resultFramework.nodes[0].parentNodeId = "MISSING-PARENT";
    input.resultFramework.nodes[0].nodeType = "Output";
    input.monitoringPlan.kpis[0].unitOfMeasure = "";
    input.implementation.activities[0].responsibleRole = "";
    input.evidenceReferences.push({ evidenceId: "EVID-1", referenceNumber: "REF-DUPLICATE", verificationStatus: "Verified" });
  }

  return {
    actionName: `${meta.caseId} NPD regulatory compliance evaluation`,
    caseId: meta.caseId,
    inputJson: JSON.stringify(input, null, 2),
  };
}

const INDICATORS = [
  "LegalDispute",
  "UnauthorizedOccupation",
  "UnauthorizedConstruction",
  "FamilyOrInheritanceClaim",
  "MultipleClaimants",
  "UnresolvedObjection",
  "PreviousIllegalLandActivity",
] as const;

export function getScreeningPresetJson(presetKey: ProposalPresetKey): string {
  const meta = PROPOSAL_PRESETS[presetKey];
  const recordedAtUtc = "2026-10-12T10:00:00Z";
  const indicators = INDICATORS.map((indicatorType) => {
    let evidenceState = "VerifiedAbsent";
    if (presetKey === "proposal-2" && indicatorType === "LegalDispute") {
      evidenceState = "VerifiedPresent";
    } else if (
      presetKey === "proposal-3" &&
      (indicatorType === "LegalDispute" || indicatorType === "UnauthorizedOccupation")
    ) {
      evidenceState = "VerifiedPresent";
    }
    return {
      indicatorType,
      evidenceState,
      evidenceReference: `SyntheticPp1Seed:${meta.caseId}:${indicatorType}`,
      recordedAtUtc,
    };
  });

  return JSON.stringify(
    {
      caseId: meta.caseId,
      inputVersion: "pp1-seed-contract-v2",
      indicators,
    },
    null,
    2,
  );
}

export function getComplaintPreset(presetKey: ProposalPresetKey): {
  caseId: string;
  complaintText: string;
} {
  if (presetKey === "proposal-1") {
    return {
      caseId: "PP1-FINAL-CLEAR-001",
      complaintText:
        "The divisional secretariat officer delayed issuing the formal receipt and bypassed the standard register sequence during document intake.",
    };
  }
  if (presetKey === "proposal-2") {
    return {
      caseId: "PP1-FINAL-COMPLAINT-001",
      complaintText:
        "Lease revenue payments were recorded, but an enforcement notice still requests the same amount.",
    };
  }
  return {
    caseId: "PP1-FINAL-COMBINED-001",
    complaintText:
      "Protected land may have been transferred and occupied without authorization while review remains unresolved.",
  };
}

export function getStructuredEvaluationPreset(
  capability: "risk" | "explain" | "consensus" | "conditions",
  presetKey: ProposalPresetKey,
): { actionName: string; inputJson: string } {
  const meta = PROPOSAL_PRESETS[presetKey];

  if (capability === "risk") {
    if (presetKey === "proposal-1") {
      return {
        actionName: `${meta.caseId} baseline governance risk assessment`,
        inputJson: JSON.stringify(
          {
            subjectId: meta.caseId,
            decisionHistory: [
              {
                decisionId: "DEC-CLR-01",
                subjectId: meta.caseId,
                officerId: "Officer-A",
                institutionId: "DS-HAMBANTOTA",
                decisionType: "Scrutiny",
                timestamp: "2026-08-05T09:00:00Z",
                isOverride: false,
                isException: false,
              },
            ],
            complianceViolations: [],
            governanceConflicts: [],
            complaints: [],
            institutionalValidations: [
              {
                validationId: "VAL-CLR-01",
                subjectId: meta.caseId,
                institutionId: "CEA",
                isValidated: true,
                failureReason: "",
                timestamp: "2026-08-10T10:00:00Z",
              },
            ],
          },
          null,
          2,
        ),
      };
    }

    if (presetKey === "proposal-2") {
      return {
        actionName: `${meta.caseId} referral governance risk assessment`,
        inputJson: JSON.stringify(
          {
            subjectId: meta.caseId,
            decisionHistory: [
              {
                decisionId: "DEC-REF-01",
                subjectId: meta.caseId,
                officerId: "Officer-A",
                institutionId: "DS-HAMBANTOTA",
                decisionType: "Override",
                timestamp: "2026-08-12T10:00:00Z",
                isOverride: true,
                isException: false,
              },
              {
                decisionId: "DEC-REF-02",
                subjectId: meta.caseId,
                officerId: "Officer-B",
                institutionId: "LCGD",
                decisionType: "Override",
                timestamp: "2026-08-19T11:30:00Z",
                isOverride: true,
                isException: false,
              },
            ],
            complianceViolations: [],
            governanceConflicts: [],
            complaints: [],
            institutionalValidations: [
              {
                validationId: "VAL-REF-01",
                subjectId: meta.caseId,
                institutionId: "SURVEY-DEPT",
                isValidated: false,
                failureReason: "Boundary dispute under active verification.",
                timestamp: "2026-08-20T09:00:00Z",
              },
              {
                validationId: "VAL-REF-02",
                subjectId: meta.caseId,
                institutionId: "PLC-DLC",
                isValidated: false,
                failureReason: "Awaiting boundary dispute resolution.",
                timestamp: "2026-08-22T09:00:00Z",
              },
            ],
          },
          null,
          2,
        ),
      };
    }

    return {
      actionName: `${meta.caseId} escalated multi-signal risk assessment`,
      inputJson: JSON.stringify(
        {
          subjectId: meta.caseId,
          decisionHistory: [
            {
              decisionId: "DEC-COMB-01",
              subjectId: meta.caseId,
              officerId: "Officer-A",
              institutionId: "DS-HAMBANTOTA",
              decisionType: "Approval",
              timestamp: "2026-09-01T08:00:00Z",
              isOverride: true,
              isException: true,
            },
            {
              decisionId: "DEC-COMB-02",
              subjectId: meta.caseId,
              officerId: "Officer-A",
              institutionId: "LCGD",
              decisionType: "Approval",
              timestamp: "2026-09-01T12:00:00Z",
              isOverride: true,
              isException: true,
            },
            {
              decisionId: "DEC-COMB-03",
              subjectId: meta.caseId,
              officerId: "Officer-A",
              institutionId: "LCGD",
              decisionType: "Approval",
              timestamp: "2026-09-01T16:00:00Z",
              isOverride: false,
              isException: false,
            },
          ],
          complianceViolations: [
            {
              violationId: "VIO-COMB-01",
              subjectId: meta.caseId,
              ruleId: "NPD-ZONE-01",
              category: "EnvironmentalSensitivity",
              timestamp: "2026-09-02T09:00:00Z",
            },
            {
              violationId: "VIO-COMB-02",
              subjectId: meta.caseId,
              ruleId: "NPD-BOUNDARY-02",
              category: "BoundaryDispute",
              timestamp: "2026-09-02T10:00:00Z",
            },
          ],
          governanceConflicts: [
            {
              conflictId: "CNF-COMB-01",
              subjectId: meta.caseId,
              conflictType: "LandUseIncompatibility",
              severity: "Critical",
              timestamp: "2026-09-03T11:00:00Z",
            },
          ],
          complaints: [
            {
              complaintId: "CMP-COMB-01",
              subjectId: meta.caseId,
              filingTimestamp: "2026-09-04T09:30:00Z",
              category: "UnauthorizedOccupation",
              severityLevel: "High",
              verificationStatus: "Verified",
            },
          ],
          institutionalValidations: [
            {
              validationId: "VAL-COMB-01",
              subjectId: meta.caseId,
              institutionId: "CEA",
              isValidated: false,
              failureReason: "Protected zone overlap clearance missing.",
              timestamp: "2026-09-05T10:00:00Z",
            },
            {
              validationId: "VAL-COMB-02",
              subjectId: meta.caseId,
              institutionId: "SURVEY-DEPT",
              isValidated: false,
              failureReason: "Disputed boundary pegs unverified.",
              timestamp: "2026-09-05T14:00:00Z",
            },
          ],
        },
        null,
        2,
      ),
    };
  }

  if (capability === "consensus") {
    if (presetKey === "proposal-1") {
      return {
        actionName: `${meta.caseId} multi-institution consensus check`,
        inputJson: JSON.stringify(
          {
            subjectId: meta.caseId,
            positions: [
              {
                institutionId: "DS-HAMBANTOTA",
                position: "Approve",
                authorityRole: "DivisionalSecretariat",
                reasonCode: "DS-CLEAR",
                summaryNotes: "Divisional scrutiny and field check complete.",
                submittedTimestamp: "2026-08-10T09:00:00Z",
              },
              {
                institutionId: "CEA",
                position: "Approve",
                authorityRole: "EnvironmentalAuthority",
                reasonCode: "ENV-CLEAR",
                summaryNotes: "No environmental restriction overlap.",
                submittedTimestamp: "2026-08-14T10:00:00Z",
              },
              {
                institutionId: "SURVEY-DEPT",
                position: "Approve",
                authorityRole: "SurveyAuthority",
                reasonCode: "SURV-CLEAR",
                summaryNotes: "Survey plan boundary verified.",
                submittedTimestamp: "2026-08-18T11:00:00Z",
              },
            ],
            policy: {
              mode: "Supermajority",
              expectedInstitutionIds: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
              requiredPercentage: 66.0,
              minQuorumPercentage: 66.0,
              allowConditionalAsApproval: true,
              countAbstentionsInQuorum: true,
              isRejectionBlocking: true,
              mandatoryInstitutionIds: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
            },
          },
          null,
          2,
        ),
      };
    }

    if (presetKey === "proposal-2") {
      return {
        actionName: `${meta.caseId} pending consensus check`,
        inputJson: JSON.stringify(
          {
            subjectId: meta.caseId,
            positions: [
              {
                institutionId: "DS-HAMBANTOTA",
                position: "ConditionalApprove",
                authorityRole: "DivisionalSecretariat",
                reasonCode: "DS-COND-DISPUTE",
                summaryNotes: "Subject to boundary dispute resolution.",
                submittedTimestamp: "2026-08-15T09:00:00Z",
              },
              {
                institutionId: "CEA",
                position: "Approve",
                authorityRole: "EnvironmentalAuthority",
                reasonCode: "ENV-CLEAR",
                summaryNotes: "Environmental check complete.",
                submittedTimestamp: "2026-08-18T10:00:00Z",
              },
              {
                institutionId: "SURVEY-DEPT",
                position: "Pending",
                authorityRole: "SurveyAuthority",
                reasonCode: "SURV-PENDING",
                summaryNotes: "Re-survey ordered due to boundary claim.",
                submittedTimestamp: "2026-08-20T11:00:00Z",
              },
            ],
            policy: {
              mode: "Unanimous",
              expectedInstitutionIds: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
              requiredPercentage: 100.0,
              minQuorumPercentage: 100.0,
              allowConditionalAsApproval: false,
              countAbstentionsInQuorum: true,
              isRejectionBlocking: true,
              mandatoryInstitutionIds: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
            },
          },
          null,
          2,
        ),
      };
    }

    return {
      actionName: `${meta.caseId} blocked consensus evaluation`,
      inputJson: JSON.stringify(
        {
          subjectId: meta.caseId,
          positions: [
            {
              institutionId: "DS-HAMBANTOTA",
              position: "ConditionalApprove",
              authorityRole: "DivisionalSecretariat",
              reasonCode: "DS-COND-01",
              summaryNotes: "Forwarded with occupancy warning.",
              submittedTimestamp: "2026-09-01T09:00:00Z",
            },
            {
              institutionId: "CEA",
              position: "Reject",
              authorityRole: "EnvironmentalAuthority",
              reasonCode: "ENV-PROTECTED-OVERLAP",
              summaryNotes: "Parcel overlaps PP1-SENSITIVE-ZONE without buffer clearance.",
              submittedTimestamp: "2026-09-03T10:00:00Z",
            },
            {
              institutionId: "SURVEY-DEPT",
              position: "Reject",
              authorityRole: "SurveyAuthority",
              reasonCode: "SURV-ENCROACHMENT",
              summaryNotes: "Unauthorized occupation encroaches on state reserve.",
              submittedTimestamp: "2026-09-04T11:00:00Z",
            },
          ],
          policy: {
            mode: "Supermajority",
            expectedInstitutionIds: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
            requiredPercentage: 66.0,
            minQuorumPercentage: 66.0,
            allowConditionalAsApproval: true,
            countAbstentionsInQuorum: true,
            isRejectionBlocking: true,
            mandatoryInstitutionIds: ["DS-HAMBANTOTA", "CEA", "SURVEY-DEPT"],
          },
        },
        null,
        2,
      ),
    };
  }

  if (capability === "conditions") {
    if (presetKey === "proposal-1") {
      return {
        actionName: `${meta.caseId} prerequisite condition verification`,
        inputJson: JSON.stringify(
          {
            subjectId: meta.caseId,
            conditions: [
              {
                conditionId: "COND-SURV-01",
                title: "Verified Survey Plan",
                category: "Cadastral",
                isMandatory: true,
                maxEvidenceAgeDays: 180,
                description: "Certified survey plan confirming parcel boundaries.",
              },
              {
                conditionId: "COND-ENV-01",
                title: "Environmental Screening Clearance",
                category: "Environmental",
                isMandatory: true,
                maxEvidenceAgeDays: 180,
                description: "CEA confirmation of no sensitive-zone conflict.",
              },
            ],
            evidence: [
              {
                conditionId: "COND-SURV-01",
                evidenceId: "EV-SURV-CLR-01",
                providedStatus: "Satisfied",
                evidenceTimestamp: "2026-09-15T09:00:00Z",
                expiryTimestamp: "2027-09-15T09:00:00Z",
                issuerOrAuthority: "Survey Department",
                remarks: "Survey boundary confirmed.",
              },
              {
                conditionId: "COND-ENV-01",
                evidenceId: "EV-ENV-CLR-01",
                providedStatus: "Satisfied",
                evidenceTimestamp: "2026-09-16T09:00:00Z",
                expiryTimestamp: "2027-09-16T09:00:00Z",
                issuerOrAuthority: "Central Environmental Authority",
                remarks: "Environmental clearance issued.",
              },
            ],
            policy: {
              allowProvisionalVerification: true,
            },
          },
          null,
          2,
        ),
      };
    }

    if (presetKey === "proposal-2") {
      return {
        actionName: `${meta.caseId} pending dispute condition verification`,
        inputJson: JSON.stringify(
          {
            subjectId: meta.caseId,
            conditions: [
              {
                conditionId: "COND-LEGAL-01",
                title: "Boundary Dispute Resolution Certificate",
                category: "Legal",
                isMandatory: true,
                maxEvidenceAgeDays: 90,
                description: "Formal resolution of competing boundary claim.",
              },
              {
                conditionId: "COND-ENV-01",
                title: "Environmental Screening Clearance",
                category: "Environmental",
                isMandatory: true,
                maxEvidenceAgeDays: 180,
                description: "CEA clearance certificate.",
              },
            ],
            evidence: [
              {
                conditionId: "COND-LEGAL-01",
                evidenceId: "EV-LEGAL-REF-01",
                providedStatus: "Pending",
                evidenceTimestamp: "2026-09-20T09:00:00Z",
                issuerOrAuthority: "Divisional Secretariat Legal Unit",
                remarks: "Referred to Commissioner review process.",
              },
              {
                conditionId: "COND-ENV-01",
                evidenceId: "EV-ENV-REF-01",
                providedStatus: "Satisfied",
                evidenceTimestamp: "2026-09-18T09:00:00Z",
                expiryTimestamp: "2027-09-18T09:00:00Z",
                issuerOrAuthority: "Central Environmental Authority",
                remarks: "Clearance valid.",
              },
            ],
            policy: {
              allowProvisionalVerification: true,
            },
          },
          null,
          2,
        ),
      };
    }

    return {
      actionName: `${meta.caseId} escalated condition verification`,
      inputJson: JSON.stringify(
        {
          subjectId: meta.caseId,
          conditions: [
            {
              conditionId: "COND-ENV-BUFFER-01",
              title: "Sensitive Zone Buffer Clearance",
              category: "Environmental",
              isMandatory: true,
              maxEvidenceAgeDays: 90,
              description: "Mandatory clearance for PP1-SENSITIVE-ZONE overlap.",
            },
            {
              conditionId: "COND-OCC-CLEAR-01",
              title: "Vacant Possession Verification",
              category: "Enforcement",
              isMandatory: true,
              maxEvidenceAgeDays: 60,
              description: "Confirmation that parcel is free from unauthorized occupation.",
            },
          ],
          evidence: [
            {
              conditionId: "COND-ENV-BUFFER-01",
              evidenceId: "EV-COMB-ENV-01",
              providedStatus: "Failed",
              evidenceTimestamp: "2026-09-25T09:00:00Z",
              issuerOrAuthority: "Central Environmental Authority",
              remarks: "Protected zone overlap violates buffer policy.",
            },
            {
              conditionId: "COND-OCC-CLEAR-01",
              evidenceId: "EV-COMB-OCC-01",
              providedStatus: "Failed",
              evidenceTimestamp: "2026-09-26T09:00:00Z",
              issuerOrAuthority: "Divisional Field Inspector",
              remarks: "Active unauthorized occupation reported.",
            },
          ],
          policy: {
            allowProvisionalVerification: false,
          },
        },
        null,
        2,
      ),
    };
  }

  // capability === "explain"
  if (presetKey === "proposal-1") {
    return {
      actionName: `${meta.caseId} explainability synthesis`,
      inputJson: JSON.stringify(
        {
          subjectId: meta.caseId,
          complianceEvidence: {
            status: "Compliant",
            violatedRules: [],
            unsatisfiedConditions: [],
          },
          conflictEvidence: {
            conflicts: [],
          },
          riskEvidence: {
            overallScore: 0,
            riskSeverity: "Low",
            requiresHumanReview: false,
            triggeredIndicators: [],
          },
        },
        null,
        2,
      ),
    };
  }

  if (presetKey === "proposal-2") {
    return {
      actionName: `${meta.caseId} referral explainability synthesis`,
      inputJson: JSON.stringify(
        {
          subjectId: meta.caseId,
          complianceEvidence: {
            status: "RequiresReview",
            violatedRules: [
              {
                ruleCode: "SCR-LEGAL-DISPUTE",
                ruleCategory: "EarlyScreening",
                summaryMessage: "Recorded legal dispute requires Commissioner review before normal flow continues.",
              },
            ],
            unsatisfiedConditions: [
              "Resolve competing boundary claim before PLC/DLC recommendation.",
            ],
          },
          conflictEvidence: {
            conflicts: [],
          },
          riskEvidence: {
            overallScore: 45,
            riskSeverity: "Moderate",
            requiresHumanReview: true,
            triggeredIndicators: [
              {
                indicatorCode: "IND-REF-OVERRIDE",
                category: "RepeatedOverrideRisk",
                severity: "Moderate",
                evidenceSummary: "Two overrides recorded while survey boundary verification was pending.",
                triggeredRule: "RULE-RISK-002",
                recommendedAction: "Hold normal workflow and refer dossier to Commissioner review.",
              },
            ],
          },
        },
        null,
        2,
      ),
    };
  }

  return {
    actionName: `${meta.caseId} escalated explainability synthesis`,
    inputJson: JSON.stringify(
      {
        subjectId: meta.caseId,
        complianceEvidence: {
          status: "NonCompliant",
          violatedRules: [
            {
              ruleCode: "NPD-ZONE-01",
              ruleCategory: "EnvironmentalSensitivity",
              summaryMessage: "Parcel overlaps PP1-SENSITIVE-ZONE without environmental buffer clearance.",
            },
          ],
          unsatisfiedConditions: [
            "Obtain CEA sensitive-zone buffer clearance.",
            "Resolve unauthorized occupation complaint.",
          ],
        },
        conflictEvidence: {
          conflicts: [
            {
              conflictType: "LandUseIncompatibility",
              severity: "Critical",
              summaryMessage: "Proposed lease conflicts with protected environmental zone designation.",
              evidenceRuleCode: "RULE-CONF-ENV-01",
              recommendedAction: "Escalate to Commissioner and Ministry of Lands for mandate reconciliation.",
            },
          ],
        },
        riskEvidence: {
          overallScore: 85,
          riskSeverity: "Critical",
          requiresHumanReview: true,
          triggeredIndicators: [
            {
              indicatorCode: "IND-COMB-COMPLAINT",
              category: "ComplaintRiskIndicator",
              severity: "High",
              evidenceSummary: "Verified unauthorized occupation complaint (ML v2 probability 78.30%) combined with workflow correction loop.",
              triggeredRule: "RULE-RISK-005",
              recommendedAction: "Require independent Commissioner review and field audit before any further processing.",
            },
          ],
        },
      },
      null,
      2,
    ),
  };
}


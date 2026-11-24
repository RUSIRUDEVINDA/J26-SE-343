import type {
  LeaseInterestRequestStatus,
  LeaseInterestStatusHistoryEntry,
} from "../types/landIntelligence";

export type ApplicantWorkflowStageId =
  | "submitted"
  | "awaiting_valuation"
  | "awaiting_authority"
  | "authority_decision";

export type ApplicantProgressStepState = "complete" | "current" | "upcoming";

export type ApplicantProgressStep = {
  id: ApplicantWorkflowStageId;
  label: string;
  state: ApplicantProgressStepState;
  completedAtUtc: string | null;
};

/** One row in the applicant activity timeline (may expand a single history entry). */
export type ApplicantTimelineDisplayEntry = {
  key: string;
  label: string;
  changedAtUtc: string;
  note: string | null | undefined;
  noteStatus: LeaseInterestRequestStatus;
};

export type ApplicantWorkflowContext = {
  status: LeaseInterestRequestStatus;
  hasOfficerValuation: boolean;
  effectiveStatus: LeaseInterestRequestStatus;
  statusHistory: LeaseInterestStatusHistoryEntry[];
};

const STATUS_ORDER_FOR_TIE_BREAK: Record<number, number> = {
  2: 10,
  3: 20,
  4: 25,
  9: 30,
  6: 40,
  7: 50,
  5: 50,
  8: 55,
};

export function sortApplicantStatusHistory(
  history: LeaseInterestStatusHistoryEntry[],
): LeaseInterestStatusHistoryEntry[] {
  return [...history].sort((a, b) => {
    const ta = Date.parse(a.changedAtUtc);
    const tb = Date.parse(b.changedAtUtc);
    if (ta !== tb) {
      return ta - tb;
    }
    const oa = STATUS_ORDER_FOR_TIE_BREAK[a.status] ?? 0;
    const ob = STATUS_ORDER_FOR_TIE_BREAK[b.status] ?? 0;
    return oa - ob;
  });
}

export function historyIncludesStatus(
  history: LeaseInterestStatusHistoryEntry[],
  status: LeaseInterestRequestStatus,
): boolean {
  return history.some((entry) => entry.status === status);
}

export function firstHistoryTimestamp(
  history: LeaseInterestStatusHistoryEntry[],
  status: LeaseInterestRequestStatus,
): string | null {
  const sorted = sortApplicantStatusHistory(history);
  const match = sorted.find((entry) => entry.status === status);
  return match?.changedAtUtc ?? null;
}

export function buildApplicantWorkflowContext(
  status: LeaseInterestRequestStatus,
  statusHistory: LeaseInterestStatusHistoryEntry[],
  hasOfficerValuation: boolean,
): ApplicantWorkflowContext {
  const effectiveStatus =
    status === 9 && hasOfficerValuation ? 6 : status;
  return {
    status,
    hasOfficerValuation,
    effectiveStatus,
    statusHistory: sortApplicantStatusHistory(statusHistory),
  };
}

export function leaseInterestApplicantStatusLabel(
  ctx: ApplicantWorkflowContext,
): string {
  const status = ctx.effectiveStatus;
  switch (status) {
    case 1:
      return "Draft";
    case 2:
    case 3:
      return "Submitted — awaiting officer verification";
    case 4:
      return "Needs information";
    case 5:
      return "Rejected";
    case 6:
      return "Valuation recorded — awaiting authority decision";
    case 7:
      return "Approved in principle";
    case 8:
      return "Closed — another request approved";
    case 9:
      return "Verification complete — awaiting valuation record";
    default:
      return "Unknown";
  }
}

export function leaseInterestApplicantOutcomeMessage(
  ctx: ApplicantWorkflowContext,
): string {
  const status = ctx.effectiveStatus;
  switch (status) {
    case 1:
      return "Draft — not submitted.";
    case 2:
    case 3:
      return "Your request is with an officer for preliminary verification. This is an expression of interest only — not a lease grant or land reservation.";
    case 4:
      return "More information is required before officer verification can continue. Update your request when an officer contacts you or when resubmission is requested.";
    case 5:
      return "The authority declined this preliminary request after review. This is not a granted lease or reserved parcel.";
    case 6:
      return "An officer recorded the land valuation and referred your request for an authority decision on preliminary approval. This does not grant a lease or reserve land.";
    case 9:
      return "Preliminary verification is complete. A valuation officer will record the land worksheet next.";
    case 7:
      return "Approved in principle — a preliminary decision only. Formal lease processing is still required; this does not grant a lease or reserve land.";
    case 8:
      return "Closed — another preliminary request for this parcel was approved in principle. This is not a rejection of your proposal on its merits.";
    default:
      return "Status unknown.";
  }
}

export function leaseInterestApplicantBadgeTone(
  ctx: ApplicantWorkflowContext,
): "neutral" | "success" | "warning" | "danger" | "primary" {
  const status = ctx.effectiveStatus;
  switch (status) {
    case 1:
    case 8:
      return "neutral";
    case 2:
    case 3:
      return "primary";
    case 4:
    case 6:
    case 9:
      return "warning";
    case 5:
      return "danger";
    case 7:
      return "success";
    default:
      return "neutral";
  }
}

export function leaseInterestApplicantTimelineEventLabel(
  status: LeaseInterestRequestStatus,
): string {
  switch (status) {
    case 2:
      return "Application submitted";
    case 3:
      return "Awaiting officer verification";
    case 4:
      return "Needs information";
    case 5:
      return "Rejected";
    case 6:
      return "Valuation recorded — awaiting authority decision";
    case 7:
      return "Approved in principle";
    case 8:
      return "Closed — another request approved";
    case 9:
      return "Verification complete";
    default:
      return "Status update";
  }
}

/**
 * Applicant timeline: officer verification completion (status 9) is shown as two
 * consecutive events — verification, then queueing for valuation record.
 */
export function expandApplicantTimelineForDisplay(
  history: LeaseInterestStatusHistoryEntry[],
): ApplicantTimelineDisplayEntry[] {
  const sorted = sortApplicantStatusHistory(history);
  const rows: ApplicantTimelineDisplayEntry[] = [];

  sorted.forEach((entry, index) => {
    if (entry.status === 9) {
      rows.push({
        key: `${entry.changedAtUtc}-9-verification`,
        label: "Verification complete",
        changedAtUtc: entry.changedAtUtc,
        note: entry.note,
        noteStatus: 9,
      });
      rows.push({
        key: `${entry.changedAtUtc}-9-awaiting-valuation`,
        label: "Awaiting valuation record",
        changedAtUtc: entry.changedAtUtc,
        note: null,
        noteStatus: 9,
      });
      return;
    }

    rows.push({
      key: `${entry.changedAtUtc}-${entry.status}-${index}`,
      label: leaseInterestApplicantTimelineEventLabel(entry.status),
      changedAtUtc: entry.changedAtUtc,
      note: entry.note,
      noteStatus: entry.status,
    });
  });

  return rows;
}

export function shouldShowApplicantProgressTracker(
  ctx: ApplicantWorkflowContext,
): boolean {
  if (ctx.effectiveStatus === 1) {
    return false;
  }
  if (ctx.effectiveStatus === 4) {
    return false;
  }
  return true;
}

export function buildApplicantProgressSteps(
  ctx: ApplicantWorkflowContext,
): ApplicantProgressStep[] {
  const history = ctx.statusHistory;
  const status = ctx.effectiveStatus;

  const verificationCompleteAt = firstHistoryTimestamp(history, 9);
  const valuationRecordedAt =
    firstHistoryTimestamp(history, 6) ??
    (ctx.hasOfficerValuation
      ? history
          .filter((e) => e.status === 6)
          .map((e) => e.changedAtUtc)
          .at(-1) ?? null
      : null);
  const authorityDecisionAt =
    firstHistoryTimestamp(history, 7) ?? firstHistoryTimestamp(history, 5);

  const steps: ApplicantProgressStep[] = [
    {
      id: "submitted",
      label: "Submitted — awaiting officer verification",
      state: "upcoming",
      completedAtUtc: null,
    },
    {
      id: "awaiting_valuation",
      label: "Verification complete — awaiting valuation record",
      state: "upcoming",
      completedAtUtc: null,
    },
    {
      id: "awaiting_authority",
      label: "Valuation recorded — awaiting authority decision",
      state: "upcoming",
      completedAtUtc: null,
    },
    {
      id: "authority_decision",
      label:
        status === 7
          ? "Approved in principle"
          : status === 5
            ? "Rejected"
            : "Authority decision",
      state: "upcoming",
      completedAtUtc: null,
    },
  ];

  if (status === 8) {
    return [];
  }

  if (status === 5 && verificationCompleteAt === null && valuationRecordedAt === null) {
    steps[0].state = "complete";
    steps[0].completedAtUtc =
      firstHistoryTimestamp(history, 2) ??
      firstHistoryTimestamp(history, 3) ??
      null;
    steps[3].label = "Rejected";
    steps[3].state = "complete";
    steps[3].completedAtUtc = authorityDecisionAt;
    return steps;
  }

  const submittedComplete =
    verificationCompleteAt !== null ||
    status === 9 ||
    status === 6 ||
    status === 7 ||
    (status === 5 && verificationCompleteAt !== null);
  if (submittedComplete) {
    steps[0].state = "complete";
    steps[0].completedAtUtc =
      firstHistoryTimestamp(history, 2) ??
      firstHistoryTimestamp(history, 3) ??
      null;
  } else if (status === 2 || status === 3) {
    steps[0].state = "current";
  }

  const valuationQueueComplete =
    valuationRecordedAt !== null || status === 6 || status === 7 || status === 5;
  if (valuationQueueComplete) {
    steps[1].state = "complete";
    steps[1].completedAtUtc = verificationCompleteAt;
  } else if (status === 9) {
    steps[1].state = "current";
  }

  const authorityDecisionRecorded = status === 7 || status === 5;
  if (authorityDecisionRecorded) {
    steps[2].state = "complete";
    steps[2].completedAtUtc = valuationRecordedAt;
    steps[3].state = "complete";
    steps[3].completedAtUtc = authorityDecisionAt;
  } else if (status === 6) {
    steps[2].state = "current";
    steps[2].completedAtUtc = valuationRecordedAt;
  }

  return steps;
}

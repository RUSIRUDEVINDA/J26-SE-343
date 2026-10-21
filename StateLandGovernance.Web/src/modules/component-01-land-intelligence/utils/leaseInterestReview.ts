import type {
  LeaseInterestCostLine,
  LeaseInterestCostSummary,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";
import { isSupportedLandUseType } from "../types/landIntelligence";

export const LEASE_INTEREST_COST_GROUPS: Array<{
  title: string;
  codes: string[];
}> = [
  {
    title: "Premium and rent",
    codes: ["assessedLandValue", "upfrontPremium", "annualLeaseRent"],
  },
  {
    title: "Deposit",
    codes: ["refundableDeposit"],
  },
  {
    title: "Statutory taxes and fees",
    codes: ["stampDuty", "otherTaxesAndFees", "estimatedInitialPayment"],
  },
];

export function formatCostLineStatus(status: string): string {
  if (status === "PendingAuthorityAssessment") {
    return "Pending assessment";
  }
  return status;
}

export function formatCostLineAmount(
  line: LeaseInterestCostLine,
  summary: LeaseInterestCostSummary,
): string | null {
  if (!summary.numericEstimateAvailable) {
    return null;
  }
  if (line.amount == null || line.amount <= 0) {
    return null;
  }
  const currency = line.currency ?? summary.currency;
  const formatted = line.amount.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
  const estimateLabel = line.rateSource ? ` (${line.rateSource})` : "";
  return `${currency} ${formatted}${estimateLabel}`;
}

export function groupCostLines(
  lines: LeaseInterestCostLine[],
): Array<{ title: string; lines: LeaseInterestCostLine[] }> {
  const byCode = new Map(lines.map((line) => [line.code, line]));
  const grouped: Array<{ title: string; lines: LeaseInterestCostLine[] }> = [];

  for (const group of LEASE_INTEREST_COST_GROUPS) {
    const groupLines = group.codes
      .map((code) => byCode.get(code))
      .filter((line): line is LeaseInterestCostLine => line != null);
    if (groupLines.length > 0) {
      grouped.push({ title: group.title, lines: groupLines });
    }
  }

  const knownCodes = new Set(
    LEASE_INTEREST_COST_GROUPS.flatMap((group) => group.codes),
  );
  const remainder = lines.filter((line) => !knownCodes.has(line.code));
  if (remainder.length > 0) {
    grouped.push({ title: "Other", lines: remainder });
  }

  return grouped;
}

/** Mirrors the backend submit-time email check (draft saves allow incomplete/blank values). */
export function isPlausibleContactEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/.test(value.trim());
}

/** Mirrors the backend submit-time phone check: 7–15 digits, optional leading +, spaces, dashes, brackets. */
export function isPlausibleContactPhone(value: string): boolean {
  const trimmed = value.trim();
  if (!/^\+?[0-9 ()-]+$/.test(trimmed)) {
    return false;
  }
  const digits = trimmed.replace(/\D/g, "").length;
  return digits >= 7 && digits <= 15;
}

/**
 * Submit readiness only. Saving a draft is intentionally not gated by this check —
 * the backend accepts incomplete drafts and validates only the fields that were entered.
 */
export function isLeaseInterestDraftReadyForSubmit(
  draft: UpsertLeaseInterestDraft,
): boolean {
  if (!draft.consentToPreliminaryAssessment) {
    return false;
  }
  if (!draft.applicant.legalName.trim()) {
    return false;
  }
  if (!isPlausibleContactPhone(draft.applicant.contactPhone)) {
    return false;
  }
  if (!isPlausibleContactEmail(draft.applicant.contactEmail)) {
    return false;
  }
  if (!draft.applicant.postalAddress.trim()) {
    return false;
  }
  if (draft.applicant.kind === 2) {
    if (!draft.applicant.organizationName?.trim()) {
      return false;
    }
    if (!draft.applicant.organizationRegistrationNumber?.trim()) {
      return false;
    }
    if (!draft.applicant.representativeNationalIdentityCardNumber?.trim()) {
      return false;
    }
  } else if (!draft.applicant.nationalIdentityCardNumber?.trim()) {
    return false;
  }
  if (!isSupportedLandUseType(draft.proposal.requestedPurpose)) {
    return false;
  }
  if (!draft.proposal.plannedActivitiesDescription.trim()) {
    return false;
  }
  if (!draft.proposal.desiredStartPeriod.trim()) {
    return false;
  }
  if (!draft.proposal.projectSummary.trim()) {
    return false;
  }
  if (
    draft.proposal.requestedDurationYears < 1 ||
    draft.proposal.requestedDurationYears > 30
  ) {
    return false;
  }
  if (
    draft.proposal.requestedAreaHectares == null ||
    draft.proposal.requestedAreaHectares <= 0
  ) {
    return false;
  }
  return true;
}

export function displayOptionalText(value: string | null | undefined): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : "—";
}

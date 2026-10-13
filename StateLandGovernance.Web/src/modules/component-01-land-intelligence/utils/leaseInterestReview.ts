import type {
  LeaseInterestCostLine,
  LeaseInterestCostSummary,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";

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

export function isLeaseInterestDraftReadyForSubmit(
  draft: UpsertLeaseInterestDraft,
): boolean {
  if (!draft.consentToPreliminaryAssessment) {
    return false;
  }
  if (!draft.applicant.legalName.trim()) {
    return false;
  }
  if (!draft.applicant.contactPhone.trim()) {
    return false;
  }
  if (!draft.applicant.contactEmail.trim()) {
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

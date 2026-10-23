import type { UpsertLeaseInterestDraft } from "../types/landIntelligence";
import { isSupportedLandUseType } from "../types/landIntelligence";
import {
  isDesiredStartPeriodOnOrAfterToday,
  isPlausibleContactEmail,
  isPlausibleContactPhone,
} from "./leaseInterestReview";

export type LeaseInterestFormFieldId =
  | "legal-name"
  | "applicant-nic"
  | "org-name"
  | "org-reg"
  | "rep-nic"
  | "phone"
  | "email"
  | "address"
  | "purpose"
  | "duration"
  | "start"
  | "area"
  | "activities"
  | "summary"
  | "consent";

const MAX_LEGAL_NAME = 200;
const MAX_PHONE = 32;
const MAX_EMAIL = 254;
const MAX_ADDRESS = 500;
const MAX_ORG_NAME = 200;
const MAX_ORG_REG = 64;
const MAX_ACTIVITIES = 4000;
const MAX_START = 100;
const MAX_SUMMARY = 4000;
const MIN_DURATION = 1;
const MAX_DURATION = 30;

function setField(
  errors: Partial<Record<LeaseInterestFormFieldId, string>>,
  field: LeaseInterestFormFieldId,
  message: string,
) {
  if (!errors[field]) {
    errors[field] = message;
  }
}

function requireText(
  errors: Partial<Record<LeaseInterestFormFieldId, string>>,
  field: LeaseInterestFormFieldId,
  value: string | null | undefined,
  label: string,
  maxLength: number,
  requiredSuffix = "",
) {
  const trimmed = value?.trim() ?? "";
  if (!trimmed) {
    setField(errors, field, `${label} is required${requiredSuffix}.`);
    return;
  }
  if (trimmed.length > maxLength) {
    setField(errors, field, `${label} must be ${maxLength} characters or fewer.`);
  }
}

function normalizeNic(input: string): string | null {
  const trimmed = input.trim();
  if (!trimmed) {
    return null;
  }
  let compact = trimmed.replace(/[\s-]/g, "");
  if (compact.length === 10 && /[vx]$/i.test(compact)) {
    compact = compact.slice(0, 9) + compact.slice(9).toUpperCase();
  }
  return compact;
}

function validateNicValue(
  raw: string | null | undefined,
  required: boolean,
): string | null {
  if (!raw?.trim()) {
    return required ? "National Identity Card (NIC) number is required." : null;
  }
  const normalized = normalizeNic(raw);
  if (!normalized) {
    return required ? "National Identity Card (NIC) number is required." : null;
  }
  if (!/^\d{9}[VX]$/.test(normalized) && !/^\d{12}$/.test(normalized)) {
    return "Enter a valid Sri Lankan NIC (9 digits followed by V or X, or a 12-digit new NIC).";
  }
  return null;
}

function validateBrnValue(raw: string | null | undefined): string | null {
  if (!raw?.trim()) {
    return "Business registration number (BRN) is required.";
  }
  const compact = raw.trim().replace(/[\s\-_/]/g, "").toUpperCase();
  if (compact.length < 3 || compact.length > 64 || !/^[A-Z0-9]+$/.test(compact)) {
    return "Enter a valid business registration number (BRN) using letters and digits.";
  }
  return null;
}

export function validateLeaseInterestApplicantStep(
  draft: UpsertLeaseInterestDraft,
): Partial<Record<LeaseInterestFormFieldId, string>> {
  const errors: Partial<Record<LeaseInterestFormFieldId, string>> = {};
  const applicant = draft.applicant;

  requireText(errors, "legal-name", applicant.legalName, "Applicant Full name", MAX_LEGAL_NAME);
  requireText(errors, "address", applicant.postalAddress, "Postal address", MAX_ADDRESS);

  const phone = applicant.contactPhone?.trim() ?? "";
  if (!phone) {
    setField(errors, "phone", "Contact phone is required.");
  } else if (phone.length > MAX_PHONE) {
    setField(errors, "phone", `Contact phone must be ${MAX_PHONE} characters or fewer.`);
  } else if (!isPlausibleContactPhone(phone)) {
    setField(
      errors,
      "phone",
      "Contact phone must be a valid phone number (7–15 digits; optional +, spaces, dashes or brackets).",
    );
  }

  const email = applicant.contactEmail?.trim() ?? "";
  if (!email) {
    setField(errors, "email", "Contact email is required.");
  } else if (email.length > MAX_EMAIL) {
    setField(errors, "email", `Contact email must be ${MAX_EMAIL} characters or fewer.`);
  } else if (!isPlausibleContactEmail(email)) {
    setField(errors, "email", "Contact email must be a valid email address.");
  }

  if (applicant.kind === 2) {
    requireText(
      errors,
      "org-name",
      applicant.organizationName,
      "Organization name",
      MAX_ORG_NAME,
      " for organization applicants",
    );
    requireText(
      errors,
      "org-reg",
      applicant.organizationRegistrationNumber,
      "Organization registration number",
      MAX_ORG_REG,
      " for organization applicants",
    );
    const brnError = validateBrnValue(applicant.organizationRegistrationNumber);
    if (brnError) {
      setField(errors, "org-reg", brnError);
    }
    const repNicError = validateNicValue(
      applicant.representativeNationalIdentityCardNumber,
      false,
    );
    if (repNicError) {
      setField(errors, "rep-nic", repNicError);
    }
  } else {
    const nicError = validateNicValue(applicant.nationalIdentityCardNumber, true);
    if (nicError) {
      setField(errors, "applicant-nic", nicError);
    }
  }

  return errors;
}

export function validateLeaseInterestProposalStep(
  draft: UpsertLeaseInterestDraft,
): Partial<Record<LeaseInterestFormFieldId, string>> {
  const errors: Partial<Record<LeaseInterestFormFieldId, string>> = {};
  const proposal = draft.proposal;

  if (!isSupportedLandUseType(proposal.requestedPurpose)) {
    setField(
      errors,
      "purpose",
      "Requested purpose must be Agricultural, Residential, or Commercial.",
    );
  }

  const duration = proposal.requestedDurationYears;
  if (duration < MIN_DURATION || duration > MAX_DURATION) {
    setField(
      errors,
      "duration",
      `Requested duration must be between ${MIN_DURATION} and ${MAX_DURATION} years (requested term only — not an approved lease length).`,
    );
  }

  const start = proposal.desiredStartPeriod?.trim() ?? "";
  if (!start) {
    setField(errors, "start", "Desired start period is required.");
  } else if (start.length > MAX_START) {
    setField(errors, "start", `Desired start period must be ${MAX_START} characters or fewer.`);
  } else if (!isDesiredStartPeriodOnOrAfterToday(start)) {
    setField(errors, "start", "Desired start period cannot be in the past.");
  }

  const activities = proposal.plannedActivitiesDescription?.trim() ?? "";
  if (!activities) {
    setField(
      errors,
      "activities",
      "A description of planned activities is required.",
    );
  } else if (activities.length > MAX_ACTIVITIES) {
    setField(
      errors,
      "activities",
      `A description of planned activities must be ${MAX_ACTIVITIES} characters or fewer.`,
    );
  }

  requireText(errors, "summary", proposal.projectSummary, "Project summary", MAX_SUMMARY);

  if (
    proposal.requestedAreaHectares == null ||
    proposal.requestedAreaHectares <= 0
  ) {
    setField(errors, "area", "Requested area must be greater than zero.");
  }

  return errors;
}

export function validateLeaseInterestReviewStep(
  draft: UpsertLeaseInterestDraft,
): Partial<Record<LeaseInterestFormFieldId, string>> {
  const errors: Partial<Record<LeaseInterestFormFieldId, string>> = {};
  if (!draft.consentToPreliminaryAssessment) {
    setField(
      errors,
      "consent",
      "Consent to submit for preliminary assessment is required.",
    );
  }
  return errors;
}

/** Full client-side check before submit (all steps). */
export function validateLeaseInterestDraftForSubmit(
  draft: UpsertLeaseInterestDraft,
): Partial<Record<LeaseInterestFormFieldId, string>> {
  return {
    ...validateLeaseInterestApplicantStep(draft),
    ...validateLeaseInterestProposalStep(draft),
    ...validateLeaseInterestReviewStep(draft),
  };
}

const FIELD_STEP: Record<LeaseInterestFormFieldId, number> = {
  "legal-name": 0,
  "applicant-nic": 0,
  "org-name": 0,
  "org-reg": 0,
  "rep-nic": 0,
  phone: 0,
  email: 0,
  address: 0,
  purpose: 1,
  duration: 1,
  start: 1,
  area: 1,
  activities: 1,
  summary: 1,
  consent: 3,
};

export function firstLeaseInterestStepWithError(
  errors: Partial<Record<LeaseInterestFormFieldId, string>>,
): number | null {
  const fields = Object.keys(errors) as LeaseInterestFormFieldId[];
  if (fields.length === 0) {
    return null;
  }
  return Math.min(...fields.map((field) => FIELD_STEP[field]));
}

/** Maps API validation strings to form field ids (first match wins per field). */
export function mapLeaseInterestApiErrorsToFields(
  messages: string[],
  applicantKind: 1 | 2,
): {
  byField: Partial<Record<LeaseInterestFormFieldId, string>>;
  unmapped: string[];
} {
  const byField: Partial<Record<LeaseInterestFormFieldId, string>> = {};
  const unmapped: string[] = [];

  for (const message of messages) {
    const lower = message.toLowerCase();
    let field: LeaseInterestFormFieldId | null = null;

    if (lower.includes("applicant full name")) {
      field = "legal-name";
    } else if (lower.includes("postal address")) {
      field = "address";
    } else if (lower.includes("contact email")) {
      field = "email";
    } else if (lower.includes("contact phone")) {
      field = "phone";
    } else if (
      lower.includes("organization name") ||
      lower.includes("organisation name")
    ) {
      field = "org-name";
    } else if (
      lower.includes("business registration") ||
      lower.includes("(brn)") ||
      lower.includes("organization registration")
    ) {
      field = "org-reg";
    } else if (
      lower.includes("national identity card") ||
      (lower.includes("nic") && !lower.includes("brn"))
    ) {
      if (lower.includes("representative")) {
        field = "rep-nic";
      } else {
        field = applicantKind === 1 ? "applicant-nic" : "rep-nic";
      }
    } else if (lower.includes("requested purpose")) {
      field = "purpose";
    } else if (lower.includes("planned activities")) {
      field = "activities";
    } else if (lower.includes("desired start period")) {
      field = "start";
    } else if (lower.includes("project summary")) {
      field = "summary";
    } else if (lower.includes("requested duration")) {
      field = "duration";
    } else if (lower.includes("requested area")) {
      field = "area";
    } else if (lower.includes("consent")) {
      field = "consent";
    }

    if (field) {
      setField(byField, field, message);
    } else {
      unmapped.push(message);
    }
  }

  return { byField, unmapped };
}

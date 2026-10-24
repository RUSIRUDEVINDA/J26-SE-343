"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import {
  findLeaseInterestForParcel,
  getLeaseInterestDocumentChecklist,
  saveLeaseInterestDraft,
  startLeaseInterestDraft,
  submitLeaseInterest,
} from "../services/citizenWorkspaceApi";
import {
  getParcelById,
  getParcelConstraints,
} from "../services/landIntelligenceApi";
import {
  leaseInterestEligibilityBlock,
  leaseInterestMissingParcelAreaBlock,
} from "../utils/leaseInterestEligibility";
import { proposalWithParcelArea } from "../utils/leaseInterestProposal";
import {
  isLeaseInterestDraftStatus,
  leaseInterestApplicantOutcome,
  leaseInterestStatusLabel,
} from "../utils/leaseInterestStatus";
import type {
  LandParcelResponse,
  LeaseInterestDocumentChecklist,
  LeaseInterestRequestDetail,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";
import {
  AreaUnit,
  isSupportedLandUseType,
  LandUseType,
} from "../types/landIntelligence";
import { landUseTypeLabels, landUseTypeOptions } from "../utils/enumMappings";
import { displayOrUnknown, formatArea } from "../utils/formatters";
import { localDateInputValue } from "../utils/leaseInterestReview";
import {
  type LeaseInterestFormFieldId,
  firstLeaseInterestStepWithError,
  mapLeaseInterestApiErrorsToFields,
  validateLeaseInterestApplicantStep,
  validateLeaseInterestDraftForSubmit,
  validateLeaseInterestProposalStep,
} from "../utils/leaseInterestFormValidation";
import {
  sourceReportedClaimantCategoryDisplay,
  sourceReportedLandUseDisplay,
} from "../utils/sourceReportedCadastre";
import { useModalDialogA11y } from "../hooks/useModalDialogA11y";
import { LeaseInterestReviewStep } from "./LeaseInterestReviewStep";
import styles from "./LeaseInterestFormView.module.css";

const STEPS = ["Applicant", "Proposal", "Documents", "Review"] as const;

function emptyDraft(): UpsertLeaseInterestDraft {
  return {
    applicant: {
      kind: 1,
      legalName: "",
      contactPhone: "",
      contactEmail: "",
      postalAddress: "",
      organizationName: null,
      organizationRegistrationNumber: null,
      nationalIdentityCardNumber: null,
      representativeNationalIdentityCardNumber: null,
    },
    proposal: {
      // 0 = unset (LandUseType has no member at 0); applicant must pick a supported purpose.
      requestedPurpose: 0 as LandUseType,
      plannedActivitiesDescription: "",
      requestedAreaHectares: null,
      desiredStartPeriod: "",
      requestedDurationYears: 1,
      projectSummary: "",
    },
    supporting: { supportingNotes: "" },
    consentToPreliminaryAssessment: false,
  };
}

/** Maps stored start-period text to a value accepted by <input type="date">. */
function toDateInputValue(value: string): string {
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) {
    return trimmed;
  }
  return "";
}

function LeaseFieldError({
  fieldId,
  message,
}: {
  fieldId: string;
  message?: string;
}) {
  if (!message) {
    return null;
  }
  return (
    <p id={`${fieldId}-error`} className={styles.fieldError} role="alert">
      {message}
    </p>
  );
}

function fieldA11y(fieldId: string, message?: string) {
  return message
    ? ({ "aria-invalid": true, "aria-describedby": `${fieldId}-error` } as const)
    : {};
}

function controlClass(
  base: string,
  invalidClass: string,
  hasError: boolean,
): string {
  return hasError ? `${base} ${invalidClass}` : base;
}

export function LeaseInterestFormView({
  parcelId,
  onReviewStepChange,
}: {
  parcelId: string;
  onReviewStepChange?: (onReview: boolean) => void;
}) {
  const router = useRouter();
  const [step, setStep] = useState(0);
  const [parcel, setParcel] = useState<LandParcelResponse | null>(null);
  const [request, setRequest] = useState<LeaseInterestRequestDetail | null>(
    null,
  );
  const [draft, setDraft] = useState<UpsertLeaseInterestDraft>(emptyDraft());
  const [checklist, setChecklist] =
    useState<LeaseInterestDocumentChecklist | null>(null);
  const [loading, setLoading] = useState(true);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrorsById, setFieldErrorsById] = useState<
    Partial<Record<LeaseInterestFormFieldId, string>>
  >({});
  const [apiFieldErrors, setApiFieldErrors] = useState<string[]>([]);
  const [dirty, setDirty] = useState(false);
  const [saveNotice, setSaveNotice] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitSuccessOpen, setSubmitSuccessOpen] = useState(false);
  const [spatialConstraints, setSpatialConstraints] = useState<
    Awaited<ReturnType<typeof getParcelConstraints>>
  >([]);
  const idempotencyKeyRef = useRef<string | null>(null);
  const redirectedSubmittedRef = useRef(false);

  const readOnly = request != null && !isLeaseInterestDraftStatus(request.status);

  const clearFieldError = (field: LeaseInterestFormFieldId) => {
    setFieldErrorsById((prev) => {
      if (!prev[field]) {
        return prev;
      }
      const next = { ...prev };
      delete next[field];
      return next;
    });
  };

  const applyApiFieldErrors = (messages: string[]) => {
    const { byField, unmapped } = mapLeaseInterestApiErrorsToFields(
      messages,
      draft.applicant.kind,
    );
    setFieldErrorsById(byField);
    setApiFieldErrors(unmapped);
    const firstStep = firstLeaseInterestStepWithError(byField);
    if (firstStep != null) {
      setStep(firstStep);
    }
  };

  const applyExistingRequest = useCallback(
    (
      existing: LeaseInterestRequestDetail,
      parcelRes: LandParcelResponse,
    ) => {
      setRequest(existing);
      setDraft({
        applicant: existing.applicant,
        proposal: proposalWithParcelArea(existing.proposal, parcelRes),
        supporting: existing.supporting,
        consentToPreliminaryAssessment: existing.consentToPreliminaryAssessment,
      });
      setDirty(false);
    },
    [],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    redirectedSubmittedRef.current = false;
    try {
      const parcelRes = await getParcelById(parcelId);
      const constraintsRes = await getParcelConstraints(parcelId);
      const [existing, checklistRes] = await Promise.all([
        findLeaseInterestForParcel(parcelId),
        getLeaseInterestDocumentChecklist(),
      ]);
      setParcel(parcelRes);
      setSpatialConstraints(constraintsRes);
      setChecklist(checklistRes);

      if (existing && !isLeaseInterestDraftStatus(existing.status)) {
        redirectedSubmittedRef.current = true;
        router.replace(`/land-intelligence/my-requests/${existing.id}`);
        return;
      }

      const eligibilityRestriction = leaseInterestEligibilityBlock(
        parcelRes,
        constraintsRes,
      );
      const parcelAreaRestriction =
        leaseInterestMissingParcelAreaBlock(parcelRes);
      const loadEligibilityBlock =
        eligibilityRestriction ?? parcelAreaRestriction;

      if (existing) {
        applyExistingRequest(existing, parcelRes);
      } else if (loadEligibilityBlock) {
        setRequest(null);
        setDraft(emptyDraft());
        setDirty(false);
      } else {
        const created = await startLeaseInterestDraft(parcelId);
        applyExistingRequest(created, parcelRes);
      }
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Unable to load lease interest form.");
      }
    } finally {
      setLoading(false);
    }
  }, [parcelId, router, applyExistingRequest]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    onReviewStepChange?.(step === 3);
  }, [step, onReviewStepChange]);

  const { dialogRef: submitSuccessDialogRef } = useModalDialogA11y({
    open: submitSuccessOpen,
    onClose: () => setSubmitSuccessOpen(false),
  });

  async function handleStartRequest() {
    if (starting || request) {
      return;
    }
    setStarting(true);
    setError(null);
    setFieldErrorsById({});
    setApiFieldErrors([]);
    try {
      const created = await startLeaseInterestDraft(parcelId);
      if (!parcel) {
        const parcelRes = await getParcelById(parcelId);
        setParcel(parcelRes);
        applyExistingRequest(created, parcelRes);
      } else {
        applyExistingRequest(created, parcel);
      }
      setStep(0);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else {
        setError("Could not start a lease interest request.");
      }
    } finally {
      setStarting(false);
    }
  }

  useEffect(() => {
    if (!dirty) {
      return;
    }
    const handler = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);

  const patchDraft = (patch: Partial<UpsertLeaseInterestDraft>) => {
    setSaveNotice(null);
    setDraft((prev) => ({ ...prev, ...patch }));
    setDirty(true);
  };

  const draftPayloadForSave = (): UpsertLeaseInterestDraft => {
    if (!parcel) {
      return draft;
    }
    return {
      ...draft,
      proposal: proposalWithParcelArea(draft.proposal, parcel),
    };
  };

  const syncDraftFromDetail = (detail: LeaseInterestRequestDetail) => {
    if (!parcel) {
      setDraft({
        applicant: detail.applicant,
        proposal: detail.proposal,
        supporting: detail.supporting,
        consentToPreliminaryAssessment: detail.consentToPreliminaryAssessment,
      });
      return;
    }
    setDraft({
      applicant: detail.applicant,
      proposal: proposalWithParcelArea(detail.proposal, parcel),
      supporting: detail.supporting,
      consentToPreliminaryAssessment: detail.consentToPreliminaryAssessment,
    });
  };

  function handleContinue() {
    let stepErrors: Partial<Record<LeaseInterestFormFieldId, string>> = {};
    if (step === 0) {
      stepErrors = validateLeaseInterestApplicantStep(draft);
    } else if (step === 1) {
      stepErrors = validateLeaseInterestProposalStep(draft);
    }
    if (Object.keys(stepErrors).length > 0) {
      setFieldErrorsById(stepErrors);
      setApiFieldErrors([]);
      return;
    }
    setFieldErrorsById({});
    setApiFieldErrors([]);
    setStep(step + 1);
  }

  async function handleSaveDraft() {
    if (readOnly || saving) {
      return;
    }
    setSaving(true);
    setError(null);
    setSaveNotice(null);
    setFieldErrorsById({});
    setApiFieldErrors([]);
    try {
      let activeRequest = request;
      if (!activeRequest) {
        if (!parcel || eligibilityBlock) {
          setError(
            eligibilityBlock?.message ??
              "This parcel cannot be used for a lease interest request yet.",
          );
          return;
        }
        activeRequest = await startLeaseInterestDraft(parcelId);
        setRequest(activeRequest);
      }

      const updated = await saveLeaseInterestDraft(
        activeRequest.id,
        draftPayloadForSave(),
      );
      setRequest(updated);
      syncDraftFromDetail(updated);
      setDirty(false);
      setSaveNotice(
        "Draft saved. Resume anytime from My requests → Drafts.",
      );
    } catch (err) {
      if (err instanceof ApiClientError && err.errors?.length) {
        applyApiFieldErrors(err.errors);
      } else if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else {
        setError("Could not save draft.");
      }
    } finally {
      setSaving(false);
    }
  }

  async function handleSubmit() {
    if (!request || readOnly || submitting) {
      return;
    }
    const clientErrors = validateLeaseInterestDraftForSubmit(draft);
    if (Object.keys(clientErrors).length > 0) {
      setFieldErrorsById(clientErrors);
      setApiFieldErrors([]);
      const firstStep = firstLeaseInterestStepWithError(clientErrors);
      if (firstStep != null) {
        setStep(firstStep);
      }
      return;
    }
    if (!idempotencyKeyRef.current) {
      idempotencyKeyRef.current = crypto.randomUUID();
    }
    setSubmitting(true);
    setFieldErrorsById({});
    setApiFieldErrors([]);
    setError(null);
    try {
      const updated = await submitLeaseInterest(
        request.id,
        draftPayloadForSave(),
        idempotencyKeyRef.current,
      );
      setRequest(updated);
      setDirty(false);
      setStep(3);
      setSubmitSuccessOpen(true);
    } catch (err) {
      if (err instanceof ApiClientError && err.errors?.length) {
        applyApiFieldErrors(err.errors);
      } else if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else {
        setError("Submission failed. You can retry safely — duplicate requests are prevented.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  const landUseDisplay = useMemo(() => {
    if (!parcel) {
      return "Unavailable";
    }
    const fromCadastre = sourceReportedLandUseDisplay(parcel.cadastralSource);
    if (fromCadastre) {
      return fromCadastre;
    }
    return parcel.currentUse
      ? landUseTypeLabels[parcel.currentUse.type]
      : "Unavailable";
  }, [parcel]);

  const claimantDisplay = useMemo(
    () => sourceReportedClaimantCategoryDisplay(parcel?.cadastralSource),
    [parcel],
  );

  const eligibilityBlock = useMemo(() => {
    const restriction = leaseInterestEligibilityBlock(
      parcel,
      spatialConstraints,
    );
    if (restriction) {
      return restriction;
    }
    return leaseInterestMissingParcelAreaBlock(parcel);
  }, [parcel, spatialConstraints]);

  if (loading || redirectedSubmittedRef.current) {
    return (
      <LoadingState
        message={
          redirectedSubmittedRef.current
            ? "Opening request status…"
            : "Loading lease interest form…"
        }
      />
    );
  }

  if (error && !parcel) {
    return <ErrorMessage message={error} />;
  }

  if (!request) {
    return (
      <div className={styles.startLayout}>
        <section className={`${styles.panel} ${styles.startPanel}`} aria-labelledby="start-request-heading">
          <div className={styles.startHeader}>
            <span className={styles.startEyebrow}>Preliminary interest request</span>
            <h2 id="start-request-heading" className={styles.startTitle}>
              Before you begin
            </h2>
            <p className={styles.startLead}>
              You will provide applicant details, describe your proposed use, and
              review everything before submission.
            </p>
          </div>
          <ol className={styles.startSteps} aria-label="Request steps">
            {[
              ["Applicant", "Your identity and contact details"],
              ["Proposal", "Purpose, activities, and requested period"],
              ["Documents", "Supporting information checklist"],
              ["Review", "Check and submit your request"],
            ].map(([title, description], index) => (
              <li className={styles.startStep} key={title}>
                <span className={styles.startStepNumber} aria-hidden="true">{index + 1}</span>
                <span>
                  <strong>{title}</strong>
                  <span className={styles.startStepDescription}>{description}</span>
                </span>
              </li>
            ))}
          </ol>
          {eligibilityBlock ? (
            <p className={styles.startBlock} role="alert">
              <strong>Cannot start request.</strong> {eligibilityBlock.message}
            </p>
          ) : null}
          {error ? <ErrorMessage message={error} /> : null}
          <div className={styles.startActions}>
            <Button
              type="button"
              variant="primary"
              disabled={starting || Boolean(eligibilityBlock)}
              onClick={() => void handleStartRequest()}
            >
              {starting ? "Starting…" : "Start request"}
            </Button>
          </div>
        </section>
        <aside className={styles.startAside} aria-label="Selected parcel">
          <div className={styles.startParcelCard}>
            <span className={styles.startEyebrow}>Selected parcel</span>
            <h3 className={styles.startParcelNumber}>
              {displayOrUnknown(parcel?.identifier.cadastralNumber)}
            </h3>
            <dl className={styles.startParcelFacts}>
              <div><dt>District</dt><dd>{displayOrUnknown(parcel?.location.district)}</dd></div>
              <div><dt>Area</dt><dd>{parcel ? formatArea(parcel.area.value, parcel.area.unit) : "Unavailable"}</dd></div>
              <div><dt>Recorded land use</dt><dd>{landUseDisplay}</dd></div>
              {claimantDisplay ? (
                <div><dt>Reported claimant</dt><dd>{claimantDisplay}</dd></div>
              ) : null}
            </dl>
            <Button
              href={`/land-intelligence/parcels/${parcelId}`}
              variant="ghost"
              className={styles.sideParcelAction}
            >
              View parcel details
            </Button>
          </div>
        </aside>
      </div>
    );
  }

  return (
    <div className={styles.layout}>
      <section className={styles.panel} aria-label="Lease interest form">
        {eligibilityBlock && !readOnly ? (
          <p className={styles.notice} role="alert">
            <strong>Submission blocked.</strong> {eligibilityBlock.message}
          </p>
        ) : null}

        {readOnly ? (
          <>
            <p className={styles.notice} role="status">
              Reference:{" "}
              <strong>{request.acknowledgementReference ?? "—"}</strong> ·
              Status:{" "}
              <strong>{leaseInterestStatusLabel(request.status)}</strong>
            </p>
            {leaseInterestApplicantOutcome(request.status).trim() ? (
              <p className={styles.notice} role="note">
                {leaseInterestApplicantOutcome(request.status)}
              </p>
            ) : null}
            {request.statusHistory.length > 0 ? (
              <div className={styles.reviewCard}>
                <h3 className={styles.reviewCardTitle}>Status timeline</h3>
                <ul className={styles.metaList}>
                  {request.statusHistory.map((entry, index) => (
                    <li key={`${entry.changedAtUtc}-${index}`}>
                      <strong>{leaseInterestStatusLabel(entry.status)}</strong>
                      {" · "}
                      {new Date(entry.changedAtUtc).toLocaleString()}
                      {entry.note ? ` — ${entry.note}` : ""}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </>
        ) : null}

        <nav className={styles.stepTrail} aria-label="Form progress">
          <ol className={styles.steps}>
            {STEPS.map((label, index) => (
              <li key={label} className={styles.stepItem}>
                {index > 0 ? (
                  <span className={styles.stepSeparator} aria-hidden="true">
                    →
                  </span>
                ) : null}
                <button
                  type="button"
                  className={`${styles.step} ${step === index ? styles.stepActive : ""} ${step > index ? styles.stepComplete : ""}`}
                  aria-current={step === index ? "step" : undefined}
                  onClick={() => setStep(index)}
                >
                  {label}
                </button>
              </li>
            ))}
          </ol>
        </nav>

        {error ? <ErrorMessage message={error} /> : null}
        {saveNotice ? (
          <p className={styles.saveNotice} role="status">
            {saveNotice}
          </p>
        ) : null}
        {apiFieldErrors.length > 0 ? (
          <ul className={styles.error} role="alert">
            {apiFieldErrors.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        ) : null}

        {step === 0 ? (
          <div className={styles.grid}>
            <div className={styles.field}>
              <label htmlFor="applicant-kind">Applicant type</label>
              <select
                id="applicant-kind"
                className={styles.select}
                disabled={readOnly}
                value={draft.applicant.kind}
                onChange={(e) => {
                  setFieldErrorsById((prev) => {
                    const next = { ...prev };
                    delete next["applicant-nic"];
                    delete next["org-name"];
                    delete next["org-reg"];
                    delete next["rep-nic"];
                    return next;
                  });
                  patchDraft({
                    applicant: {
                      ...draft.applicant,
                      kind: Number(e.target.value) as 1 | 2,
                    },
                  });
                }}
              >
                <option value={1}>Individual</option>
                <option value={2}>Organization</option>
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="legal-name">Full name</label>
              <input
                id="legal-name"
                {...fieldA11y("legal-name", fieldErrorsById["legal-name"])}
                className={controlClass(
                  styles.input,
                  styles.inputInvalid,
                  Boolean(fieldErrorsById["legal-name"]),
                )}
                disabled={readOnly}
                value={draft.applicant.legalName}
                onChange={(e) => {
                  clearFieldError("legal-name");
                  patchDraft({
                    applicant: { ...draft.applicant, legalName: e.target.value },
                  });
                }}
              />
              <LeaseFieldError
                fieldId="legal-name"
                message={fieldErrorsById["legal-name"]}
              />
            </div>
            {draft.applicant.kind === 1 ? (
              <div className={`${styles.field} ${styles.full}`}>
                <label htmlFor="applicant-nic">
                  National Identity Card (NIC) number
                </label>
                <input
                  id="applicant-nic"
                  {...fieldA11y("applicant-nic", fieldErrorsById["applicant-nic"])}
                  className={controlClass(
                    styles.input,
                    styles.inputInvalid,
                    Boolean(fieldErrorsById["applicant-nic"]),
                  )}
                  disabled={readOnly}
                  value={draft.applicant.nationalIdentityCardNumber ?? ""}
                  autoComplete="off"
                  spellCheck={false}
                  onChange={(e) => {
                    clearFieldError("applicant-nic");
                    patchDraft({
                      applicant: {
                        ...draft.applicant,
                        nationalIdentityCardNumber: e.target.value,
                      },
                    });
                  }}
                />
                <LeaseFieldError
                  fieldId="applicant-nic"
                  message={fieldErrorsById["applicant-nic"]}
                />
              </div>
            ) : null}
            {draft.applicant.kind === 2 ? (
              <>
                <div className={styles.field}>
                  <label htmlFor="org-name">Organization name</label>
                  <input
                    id="org-name"
                    {...fieldA11y("org-name", fieldErrorsById["org-name"])}
                    className={controlClass(
                      styles.input,
                      styles.inputInvalid,
                      Boolean(fieldErrorsById["org-name"]),
                    )}
                    disabled={readOnly}
                    value={draft.applicant.organizationName ?? ""}
                    onChange={(e) => {
                      clearFieldError("org-name");
                      patchDraft({
                        applicant: {
                          ...draft.applicant,
                          organizationName: e.target.value,
                        },
                      });
                    }}
                  />
                  <LeaseFieldError
                    fieldId="org-name"
                    message={fieldErrorsById["org-name"]}
                  />
                </div>
                <div className={styles.field}>
                  <label htmlFor="org-reg">Business registration number (BRN)</label>
                  <input
                    id="org-reg"
                    {...fieldA11y("org-reg", fieldErrorsById["org-reg"])}
                    className={controlClass(
                      styles.input,
                      styles.inputInvalid,
                      Boolean(fieldErrorsById["org-reg"]),
                    )}
                    disabled={readOnly}
                    value={draft.applicant.organizationRegistrationNumber ?? ""}
                    onChange={(e) => {
                      clearFieldError("org-reg");
                      patchDraft({
                        applicant: {
                          ...draft.applicant,
                          organizationRegistrationNumber: e.target.value,
                        },
                      });
                    }}
                  />
                  <LeaseFieldError
                    fieldId="org-reg"
                    message={fieldErrorsById["org-reg"]}
                  />
                </div>
                <div className={`${styles.field} ${styles.full}`}>
                  <label htmlFor="rep-nic">
                    Representative NIC (optional)
                  </label>
                  <input
                    id="rep-nic"
                    {...fieldA11y("rep-nic", fieldErrorsById["rep-nic"])}
                    className={controlClass(
                      styles.input,
                      styles.inputInvalid,
                      Boolean(fieldErrorsById["rep-nic"]),
                    )}
                    disabled={readOnly}
                    value={
                      draft.applicant.representativeNationalIdentityCardNumber ?? ""
                    }
                    autoComplete="off"
                    spellCheck={false}
                    onChange={(e) => {
                      clearFieldError("rep-nic");
                      patchDraft({
                        applicant: {
                          ...draft.applicant,
                          representativeNationalIdentityCardNumber:
                            e.target.value,
                        },
                      });
                    }}
                  />
                  <LeaseFieldError
                    fieldId="rep-nic"
                    message={fieldErrorsById["rep-nic"]}
                  />
                </div>
              </>
            ) : null}
            <div className={styles.field}>
              <label htmlFor="phone">Contact phone</label>
              <input
                id="phone"
                {...fieldA11y("phone", fieldErrorsById.phone)}
                className={controlClass(
                  styles.input,
                  styles.inputInvalid,
                  Boolean(fieldErrorsById.phone),
                )}
                disabled={readOnly}
                value={draft.applicant.contactPhone}
                onChange={(e) => {
                  clearFieldError("phone");
                  patchDraft({
                    applicant: { ...draft.applicant, contactPhone: e.target.value },
                  });
                }}
              />
              <LeaseFieldError fieldId="phone" message={fieldErrorsById.phone} />
            </div>
            <div className={styles.field}>
              <label htmlFor="email">Email</label>
              <input
                id="email"
                {...fieldA11y("email", fieldErrorsById.email)}
                type="email"
                className={controlClass(
                  styles.input,
                  styles.inputInvalid,
                  Boolean(fieldErrorsById.email),
                )}
                disabled={readOnly}
                value={draft.applicant.contactEmail}
                onChange={(e) => {
                  clearFieldError("email");
                  patchDraft({
                    applicant: { ...draft.applicant, contactEmail: e.target.value },
                  });
                }}
              />
              <LeaseFieldError fieldId="email" message={fieldErrorsById.email} />
            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="address">Postal address</label>
              <textarea
                id="address"
                {...fieldA11y("address", fieldErrorsById.address)}
                className={controlClass(
                  styles.textarea,
                  styles.textareaInvalid,
                  Boolean(fieldErrorsById.address),
                )}
                disabled={readOnly}
                value={draft.applicant.postalAddress}
                onChange={(e) => {
                  clearFieldError("address");
                  patchDraft({
                    applicant: {
                      ...draft.applicant,
                      postalAddress: e.target.value,
                    },
                  });
                }}
              />
              <LeaseFieldError
                fieldId="address"
                message={fieldErrorsById.address}
              />
            </div>
          </div>
        ) : null}

        {step === 1 ? (
          <div className={styles.grid}>
            <div className={styles.field}>
              <label htmlFor="purpose">Requested purpose</label>
              <select
                id="purpose"
                {...fieldA11y("purpose", fieldErrorsById.purpose)}
                className={controlClass(
                  styles.select,
                  styles.selectInvalid,
                  Boolean(fieldErrorsById.purpose),
                )}
                disabled={readOnly}
                value={
                  isSupportedLandUseType(draft.proposal.requestedPurpose)
                    ? draft.proposal.requestedPurpose
                    : ""
                }
                onChange={(e) => {
                  clearFieldError("purpose");
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      requestedPurpose:
                        e.target.value === ""
                          ? (0 as LandUseType)
                          : (Number(e.target.value) as LandUseType),
                    },
                  });
                }}
              >
                <option value="">Select purpose</option>
                {landUseTypeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <LeaseFieldError
                fieldId="purpose"
                message={fieldErrorsById.purpose}
              />
            </div>
            <div className={styles.field}>
              <label htmlFor="duration">Requested duration (years)</label>
              <input
                id="duration"
                {...fieldA11y("duration", fieldErrorsById.duration)}
                type="number"
                min={1}
                max={30}
                className={controlClass(
                  styles.input,
                  styles.inputInvalid,
                  Boolean(fieldErrorsById.duration),
                )}
                disabled={readOnly}
                value={draft.proposal.requestedDurationYears}
                onChange={(e) => {
                  clearFieldError("duration");
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      requestedDurationYears: Number(e.target.value),
                    },
                  });
                }}
              />
              <LeaseFieldError
                fieldId="duration"
                message={fieldErrorsById.duration}
              />
            </div>
            <div className={styles.field}>
              <label htmlFor="start">Desired start period</label>
              <input
                id="start"
                {...fieldA11y("start", fieldErrorsById.start)}
                type="date"
                min={localDateInputValue()}
                className={controlClass(
                  styles.input,
                  styles.inputInvalid,
                  Boolean(fieldErrorsById.start),
                )}
                disabled={readOnly}
                value={toDateInputValue(draft.proposal.desiredStartPeriod)}
                onChange={(e) => {
                  clearFieldError("start");
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      desiredStartPeriod: e.target.value,
                    },
                  });
                }}
              />
              <LeaseFieldError fieldId="start" message={fieldErrorsById.start} />
            </div>
            <div className={styles.field}>
              <label htmlFor="area">Requested area (ha)</label>
              <input
                id="area"
                type="text"
                readOnly
                className={`${styles.input} ${styles.inputReadOnly}`}
                disabled={readOnly}
                value={
                  draft.proposal.requestedAreaHectares != null &&
                  draft.proposal.requestedAreaHectares > 0
                    ? formatArea(
                        draft.proposal.requestedAreaHectares,
                        AreaUnit.Hectares,
                      )
                    : "—"
                }
                aria-readonly="true"
              />
              <LeaseFieldError fieldId="area" message={fieldErrorsById.area} />
            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="activities">Planned activities</label>
              <textarea
                id="activities"
                {...fieldA11y("activities", fieldErrorsById.activities)}
                className={controlClass(
                  styles.textarea,
                  styles.textareaInvalid,
                  Boolean(fieldErrorsById.activities),
                )}
                disabled={readOnly}
                value={draft.proposal.plannedActivitiesDescription}
                onChange={(e) => {
                  clearFieldError("activities");
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      plannedActivitiesDescription: e.target.value,
                    },
                  });
                }}
              />
              <LeaseFieldError
                fieldId="activities"
                message={fieldErrorsById.activities}
              />
            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="summary">Project summary</label>
              <textarea
                id="summary"
                {...fieldA11y("summary", fieldErrorsById.summary)}
                className={controlClass(
                  styles.textarea,
                  styles.textareaInvalid,
                  Boolean(fieldErrorsById.summary),
                )}
                disabled={readOnly}
                value={draft.proposal.projectSummary}
                onChange={(e) => {
                  clearFieldError("summary");
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      projectSummary: e.target.value,
                    },
                  });
                }}
              />
              <LeaseFieldError
                fieldId="summary"
                message={fieldErrorsById.summary}
              />
            </div>
          </div>
        ) : null}

        {step === 2 ? (
          <div className={styles.documentsStep}>
            <div className={styles.documentsHeading}>
              <span className={styles.documentsEyebrow}>Document guidance</span>
              <h2>What you may need</h2>
              <p>Review the supporting documents an officer may ask for during verification.</p>
            </div>
            {checklist?.items.length ? (
              <section aria-labelledby="document-guidance-heading">
                <h3 id="document-guidance-heading" className={styles.documentsSectionTitle}>
                  Supporting document guidance
                </h3>
                <ul className={styles.documentsGrid}>
                  {checklist.items.map((item, index) => (
                    <li key={item.code} className={styles.documentItem}>
                      <span className={styles.documentNumber} aria-hidden="true">
                        {String(index + 1).padStart(2, "0")}
                      </span>
                      <div>
                        <h4 className={styles.documentLabel}>{item.label}</h4>
                        <p className={styles.documentNote}>{item.note}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              </section>
            ) : null}
            <div className={`${styles.field} ${styles.documentsNotes}`}>
              <label htmlFor="notes">
                Supporting notes <span className={styles.optionalLabel}>Optional</span>
              </label>
              <p id="notes-hint" className={styles.fieldHint}>
                Add any context that may help the officer review your request.
              </p>
              <textarea
                id="notes"
                aria-describedby="notes-hint"
                className={styles.textarea}
                disabled={readOnly}
                value={draft.supporting.supportingNotes}
                onChange={(e) =>
                  patchDraft({
                    supporting: { supportingNotes: e.target.value },
                  })
                }
              />
            </div>
          </div>
        ) : null}

        {step === 3 && request && parcel ? (
          <LeaseInterestReviewStep
            parcel={parcel}
            parcelId={parcelId}
            draft={draft}
            checklist={checklist}
            readOnly={readOnly}
            onEditApplicant={() => setStep(0)}
            onEditProposal={() => setStep(1)}
            onEditDocuments={() => setStep(2)}
            onViewParcel={() =>
              router.push(`/land-intelligence/parcels/${parcelId}`)
            }
            consentChecked={draft.consentToPreliminaryAssessment}
            consentError={fieldErrorsById.consent}
            onConsentChange={(checked) => {
              clearFieldError("consent");
              patchDraft({ consentToPreliminaryAssessment: checked });
            }}
          />
        ) : null}

        {!readOnly ? (
          <div
            className={
              step === 3 ? styles.reviewActions : styles.actions
            }
          >
            {step === 3 ? (
              <>
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => setStep(2)}
                >
                  Back
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => void handleSaveDraft()}
                  disabled={saving}
                >
                  {saving ? "Saving…" : "Save draft"}
                </Button>
                <Button
                  type="button"
                  variant="primary"
                  onClick={() => void handleSubmit()}
                  disabled={submitting || eligibilityBlock !== null}
                  aria-disabled={submitting || eligibilityBlock !== null}
                >
                  {submitting
                    ? "Submitting…"
                    : "Submit for preliminary verification"}
                </Button>
              </>
            ) : (
              <>
                {step > 0 ? (
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={() => setStep(step - 1)}
                  >
                    Back
                  </Button>
                ) : null}
                {step < 3 ? (
                  <Button
                    type="button"
                    variant="primary"
                    onClick={handleContinue}
                  >
                    Continue
                  </Button>
                ) : null}
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => void handleSaveDraft()}
                  disabled={saving}
                >
                  {saving ? "Saving…" : "Save draft"}
                </Button>
              </>
            )}
          </div>
        ) : (
          <div className={styles.actions}>
            <Button href="/land-intelligence/my-requests" variant="primary">
              My requests
            </Button>
          </div>
        )}
      </section>

      <aside className={styles.sideSummary} aria-label="Parcel summary">
        {parcel ? (
          <>
            <h3 className={styles.sideSummaryTitle}>Parcel summary</h3>
            <dl className={styles.sideDl}>
              <dt>Cadastral number</dt>
              <dd className={styles.cadastralValue}>
                {displayOrUnknown(parcel.identifier.cadastralNumber)}
              </dd>
              <dt>District</dt>
              <dd>{displayOrUnknown(parcel.location.district)}</dd>
              <dt>Area</dt>
              <dd>{formatArea(parcel.area.value, parcel.area.unit)}</dd>
              <dt>Source-reported land use</dt>
              <dd>{landUseDisplay}</dd>
              {claimantDisplay ? (
                <>
                  <dt>Source-reported claimant</dt>
                  <dd>{claimantDisplay}</dd>
                </>
              ) : null}
              {parcel.availability?.projectLeaseReady ? (
                <>
                  <dt>Project designation</dt>
                  <dd>{parcel.availability.projectLeaseReady.designationLabel}</dd>
                </>
              ) : null}
            </dl>
            <div className={styles.sideActions}>
              <Button
                href={`/land-intelligence/parcels/${parcelId}/map`}
                variant="ghost"
                className={styles.sideParcelAction}
              >
                View boundary on map
              </Button>
            </div>
          </>
        ) : null}
      </aside>

      {submitSuccessOpen && request.acknowledgementReference ? (
        <div
          className={styles.modalOverlay}
          role="presentation"
          onClick={() => setSubmitSuccessOpen(false)}
        >
          <div
            ref={submitSuccessDialogRef}
            className={styles.modalDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="lease-interest-submit-success-title"
            aria-describedby="lease-interest-submit-success-body"
            onClick={(e) => e.stopPropagation()}
          >
            <h2
              id="lease-interest-submit-success-title"
              className={styles.successTitle}
            >
              Submitted for preliminary verification
            </h2>
            <p id="lease-interest-submit-success-body" className={styles.successBody}>
              Your acknowledgement reference is{" "}
              <strong>{request.acknowledgementReference}</strong>. You can track
              progress from My requests.
            </p>
            <div className={styles.modalActions}>
              <Button href="/land-intelligence/my-requests" variant="primary">
                View my requests
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => setSubmitSuccessOpen(false)}
              >
                Close
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

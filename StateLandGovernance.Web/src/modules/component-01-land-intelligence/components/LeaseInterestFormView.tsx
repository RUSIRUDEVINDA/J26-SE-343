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
import { isLeaseInterestDraftReadyForSubmit } from "../utils/leaseInterestReview";
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
  const [fieldErrors, setFieldErrors] = useState<string[]>([]);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitSuccessOpen, setSubmitSuccessOpen] = useState(false);
  const [spatialConstraints, setSpatialConstraints] = useState<
    Awaited<ReturnType<typeof getParcelConstraints>>
  >([]);
  const idempotencyKeyRef = useRef<string | null>(null);
  const redirectedSubmittedRef = useRef(false);

  const readOnly = request != null && !isLeaseInterestDraftStatus(request.status);
  const fieldErrorA11y =
    fieldErrors.length > 0
      ? ({ "aria-invalid": true, "aria-describedby": "lease-interest-field-errors" } as const)
      : {};

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

      if (existing) {
        applyExistingRequest(existing, parcelRes);
      } else {
        setRequest(null);
        setDraft(emptyDraft());
        setDirty(false);
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
    setFieldErrors([]);
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
    setDraft((prev) => ({ ...prev, ...patch }));
    setDirty(true);
  };

  async function handleSaveDraft() {
    if (!request || readOnly || saving) {
      return;
    }
    setSaving(true);
    setFieldErrors([]);
    try {
      const updated = await saveLeaseInterestDraft(request.id, draft);
      setRequest(updated);
      setDirty(false);
    } catch (err) {
      if (err instanceof ApiClientError && err.errors?.length) {
        setFieldErrors(err.errors);
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
    if (!idempotencyKeyRef.current) {
      idempotencyKeyRef.current = crypto.randomUUID();
    }
    setSubmitting(true);
    setFieldErrors([]);
    setError(null);
    try {
      const updated = await submitLeaseInterest(
        request.id,
        draft,
        idempotencyKeyRef.current,
      );
      setRequest(updated);
      setDirty(false);
      setStep(3);
      setSubmitSuccessOpen(true);
    } catch (err) {
      if (err instanceof ApiClientError && err.errors?.length) {
        setFieldErrors(err.errors);
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

  const canSubmit = useMemo(
    () =>
      isLeaseInterestDraftReadyForSubmit(draft) && eligibilityBlock === null,
    [draft, eligibilityBlock],
  );

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
            <Button href={`/land-intelligence/parcels/${parcelId}`} variant="ghost">
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
            <p className={styles.notice} role="note">
              {leaseInterestApplicantOutcome(request.status)}
            </p>
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
        {fieldErrors.length > 0 ? (
          <ul id="lease-interest-field-errors" className={styles.error} role="alert">
            {fieldErrors.map((item) => (
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
                onChange={(e) =>
                  patchDraft({
                    applicant: {
                      ...draft.applicant,
                      kind: Number(e.target.value) as 1 | 2,
                    },
                  })
                }
              >
                <option value={1}>Individual</option>
                <option value={2}>Organization</option>
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="legal-name">Full name</label>
              <input
                id="legal-name"
                {...fieldErrorA11y}
                className={styles.input}
                disabled={readOnly}
                value={draft.applicant.legalName}
                onChange={(e) =>
                  patchDraft({
                    applicant: { ...draft.applicant, legalName: e.target.value },
                  })
                }
              />
            </div>
            {draft.applicant.kind === 1 ? (
              <div className={`${styles.field} ${styles.full}`}>
                <label htmlFor="applicant-nic">
                  National Identity Card (NIC) number
                </label>
                <input
                  id="applicant-nic"
                {...fieldErrorA11y}
                  className={styles.input}
                  disabled={readOnly}
                  value={draft.applicant.nationalIdentityCardNumber ?? ""}
                  autoComplete="off"
                  spellCheck={false}
                  onChange={(e) =>
                    patchDraft({
                      applicant: {
                        ...draft.applicant,
                        nationalIdentityCardNumber: e.target.value,
                      },
                    })
                  }
                />
              </div>
            ) : null}
            {draft.applicant.kind === 2 ? (
              <>
                <div className={styles.field}>
                  <label htmlFor="org-name">Organization name</label>
                  <input
                    id="org-name"
                {...fieldErrorA11y}
                    className={styles.input}
                    disabled={readOnly}
                    value={draft.applicant.organizationName ?? ""}
                    onChange={(e) =>
                      patchDraft({
                        applicant: {
                          ...draft.applicant,
                          organizationName: e.target.value,
                        },
                      })
                    }
                  />
                </div>
                <div className={styles.field}>
                  <label htmlFor="org-reg">Registration number</label>
                  <input
                    id="org-reg"
                {...fieldErrorA11y}
                    className={styles.input}
                    disabled={readOnly}
                    value={draft.applicant.organizationRegistrationNumber ?? ""}
                    onChange={(e) =>
                      patchDraft({
                        applicant: {
                          ...draft.applicant,
                          organizationRegistrationNumber: e.target.value,
                        },
                      })
                    }
                  />
                </div>
                <div className={`${styles.field} ${styles.full}`}>
                  <label htmlFor="rep-nic">
                    Representative NIC (signatory)
                  </label>
                  <input
                    id="rep-nic"
                {...fieldErrorA11y}
                    className={styles.input}
                    disabled={readOnly}
                    value={
                      draft.applicant.representativeNationalIdentityCardNumber ?? ""
                    }
                    autoComplete="off"
                    spellCheck={false}
                    onChange={(e) =>
                      patchDraft({
                        applicant: {
                          ...draft.applicant,
                          representativeNationalIdentityCardNumber:
                            e.target.value,
                        },
                      })
                    }
                  />

                </div>
              </>
            ) : null}
            <div className={styles.field}>
              <label htmlFor="phone">Contact phone</label>
              <input
                id="phone"
                {...fieldErrorA11y}
                className={styles.input}
                disabled={readOnly}
                value={draft.applicant.contactPhone}
                onChange={(e) =>
                  patchDraft({
                    applicant: { ...draft.applicant, contactPhone: e.target.value },
                  })
                }
              />
            </div>
            <div className={styles.field}>
              <label htmlFor="email">Email</label>
              <input
                id="email"
                {...fieldErrorA11y}
                type="email"
                className={styles.input}
                disabled={readOnly}
                value={draft.applicant.contactEmail}
                onChange={(e) =>
                  patchDraft({
                    applicant: { ...draft.applicant, contactEmail: e.target.value },
                  })
                }
              />
            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="address">Postal address</label>
              <textarea
                id="address"
                {...fieldErrorA11y}
                className={styles.textarea}
                disabled={readOnly}
                value={draft.applicant.postalAddress}
                onChange={(e) =>
                  patchDraft({
                    applicant: {
                      ...draft.applicant,
                      postalAddress: e.target.value,
                    },
                  })
                }
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
                {...fieldErrorA11y}
                className={styles.select}
                disabled={readOnly}
                value={
                  isSupportedLandUseType(draft.proposal.requestedPurpose)
                    ? draft.proposal.requestedPurpose
                    : ""
                }
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      requestedPurpose:
                        e.target.value === ""
                          ? (0 as LandUseType)
                          : (Number(e.target.value) as LandUseType),
                    },
                  })
                }
              >
                <option value="">Select purpose</option>
                {landUseTypeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="duration">Requested duration (years)</label>
              <input
                id="duration"
                {...fieldErrorA11y}
                type="number"
                min={1}
                max={30}
                className={styles.input}
                disabled={readOnly}
                value={draft.proposal.requestedDurationYears}
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      requestedDurationYears: Number(e.target.value),
                    },
                  })
                }
              />
            </div>
            <div className={styles.field}>
              <label htmlFor="start">Desired start period</label>
              <input
                id="start"
                {...fieldErrorA11y}
                type="date"
                className={styles.input}
                disabled={readOnly}
                value={toDateInputValue(draft.proposal.desiredStartPeriod)}
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      desiredStartPeriod: e.target.value,
                    },
                  })
                }
              />
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

            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="activities">Planned activities</label>
              <textarea
                id="activities"
                {...fieldErrorA11y}
                className={styles.textarea}
                disabled={readOnly}
                value={draft.proposal.plannedActivitiesDescription}
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      plannedActivitiesDescription: e.target.value,
                    },
                  })
                }
              />
            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="summary">Project summary</label>
              <textarea
                id="summary"
                {...fieldErrorA11y}
                className={styles.textarea}
                disabled={readOnly}
                value={draft.proposal.projectSummary}
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      projectSummary: e.target.value,
                    },
                  })
                }
              />
            </div>
          </div>
        ) : null}

        {step === 2 ? (
          <div>
            <p style={{ fontSize: "0.85rem", color: "var(--color-muted)" }}>
              {checklist?.storageNote}
            </p>
            <ul className={styles.metaList}>
              {checklist?.items.map((item) => (
                <li key={item.code}>
                  <strong>{item.label}</strong> — {item.note}
                </li>
              ))}
            </ul>
            <div className={`${styles.field} ${styles.full}`} style={{ marginTop: "1rem" }}>
              <label htmlFor="notes">Supporting notes</label>
              <textarea
                id="notes"
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
            request={request}
            checklist={checklist}
            readOnly={readOnly}
            onEditApplicant={() => setStep(0)}
            onEditProposal={() => setStep(1)}
            onEditDocuments={() => setStep(2)}
            onViewParcel={() =>
              router.push(`/land-intelligence/parcels/${parcelId}`)
            }
            consentChecked={draft.consentToPreliminaryAssessment}
            onConsentChange={(checked) =>
              patchDraft({ consentToPreliminaryAssessment: checked })
            }
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
                  disabled={submitting || !canSubmit}
                  aria-disabled={submitting || !canSubmit}
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
                    onClick={() => setStep(step + 1)}
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
                variant="secondary"
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

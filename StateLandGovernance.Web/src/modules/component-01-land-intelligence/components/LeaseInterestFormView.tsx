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
import { AreaUnit } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { displayOrUnknown, formatArea } from "../utils/formatters";
import { isLeaseInterestDraftReadyForSubmit } from "../utils/leaseInterestReview";
import {
  sourceReportedClaimantCategoryDisplay,
  sourceReportedLandUseDisplay,
} from "../utils/sourceReportedCadastre";
import { LeaseInterestReviewStep } from "./LeaseInterestReviewStep";
import { maskNic } from "../utils/nic";
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
      requestedPurpose: 99,
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

  useEffect(() => {
    if (!submitSuccessOpen) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setSubmitSuccessOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [submitSuccessOpen]);

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

  const projectLeaseReady = parcel?.availability?.projectLeaseReady;
  const leaseAvailabilityNotice = useMemo(() => {
    if (projectLeaseReady?.isEffectiveForLeaseFlow) {
      return {
        lead: "Lease-ready — project dataset assumption.",
        body:
          "This parcel is treated as lease-ready for the J26-SE-343 research dataset only. Expressing interest still requires officer verification and does not reserve land or create a lease. Simulated workflow status, if shown, is separate from this designation.",
      };
    }
    if (projectLeaseReady?.excludedBecauseSourceReportedPrivate) {
      return {
        lead: "Not lease-ready for this project.",
        body:
          "Source-reported Private claimant category excludes this parcel from the project lease-ready assumption. Lease interest cannot be submitted here; hard restrictions and official verification still apply.",
      };
    }
    return {
      lead: "Availability not verified.",
      body:
        "Expressing lease interest records your request for preliminary assessment. It does not reserve land, create a lease, or establish payment obligations. Your requests are tied to your signed-in applicant session; NIC is collected for officer verification only and is not used for login.",
    };
  }, [projectLeaseReady]);

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
      <div className={styles.layout}>
        <section className={styles.panel} aria-label="Start lease interest">
          <p className={styles.notice} role="note">
            <strong>{leaseAvailabilityNotice.lead}</strong>{" "}
            {leaseAvailabilityNotice.body}
          </p>
          {eligibilityBlock ? (
            <p className={styles.notice} role="alert">
              <strong>Cannot start request.</strong> {eligibilityBlock.message}
            </p>
          ) : null}
          {error ? <ErrorMessage message={error} /> : null}
          <p className={styles.notice}>
            Opening this page does not create a request. Start only when you are
            ready to fill in applicant and proposal details.
          </p>
          <div className={styles.actions}>
            <Button
              type="button"
              variant="primary"
              disabled={starting || Boolean(eligibilityBlock)}
              onClick={() => void handleStartRequest()}
            >
              {starting ? "Starting…" : "Start request"}
            </Button>
            <Button href={`/land-intelligence/parcels/${parcelId}`} variant="ghost">
              Back to parcel
            </Button>
            <Button href="/land-intelligence/my-requests" variant="ghost">
              My requests
            </Button>
          </div>
        </section>
      </div>
    );
  }

  return (
    <div className={styles.layout}>
      <section className={styles.panel} aria-label="Lease interest form">
        <p className={styles.notice} role="note">
          <strong>{leaseAvailabilityNotice.lead}</strong>{" "}
          {leaseAvailabilityNotice.body}
        </p>

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
          <ul className={styles.error} role="alert">
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
                  className={styles.input}
                  disabled={readOnly}
                  value={
                    readOnly
                      ? maskNic(draft.applicant.nationalIdentityCardNumber)
                      : (draft.applicant.nationalIdentityCardNumber ?? "")
                  }
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
                <p className={styles.fieldHint}>
                  Collected to help officers match your interest request during
                  preliminary verification. It is not used as your login identity
                  and does not prove ownership of the parcel.
                </p>
              </div>
            ) : null}
            {draft.applicant.kind === 2 ? (
              <>
                <div className={styles.field}>
                  <label htmlFor="org-name">Organization name</label>
                  <input
                    id="org-name"
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
                    className={styles.input}
                    disabled={readOnly}
                    value={
                      readOnly
                        ? maskNic(
                            draft.applicant.representativeNationalIdentityCardNumber,
                          )
                        : (draft.applicant.representativeNationalIdentityCardNumber ??
                          "")
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
                  <p className={styles.fieldHint}>
                    NIC of the authorized representative named above (full name).
                    Organization registration number stays separate.
                  </p>
                </div>
              </>
            ) : null}
            <div className={styles.field}>
              <label htmlFor="phone">Contact phone</label>
              <input
                id="phone"
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
                className={styles.select}
                disabled={readOnly}
                value={draft.proposal.requestedPurpose}
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      requestedPurpose: Number(e.target.value),
                    },
                  })
                }
              >
                {Object.entries(landUseTypeLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="duration">Requested duration (years)</label>
              <input
                id="duration"
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
              <p className={styles.fieldHint}>
                Taken from the parcel record; you cannot request a different area
                here.
              </p>
            </div>
            <div className={`${styles.field} ${styles.full}`}>
              <label htmlFor="activities">Planned activities</label>
              <textarea
                id="activities"
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
              <dt>Lease availability (official)</dt>
              <dd>
                {parcel.availability?.actualLeaseAvailabilityLabel ??
                  "Not verified"}
              </dd>
              {parcel.availability?.projectLeaseReady ? (
                <>
                  <dt>Project designation</dt>
                  <dd>{parcel.availability.projectLeaseReady.designationLabel}</dd>
                </>
              ) : null}
            </dl>
            <details className={styles.sideTechnical}>
              <summary>Technical identifiers</summary>
              <dl className={styles.sideDl}>
                <dt>Parcel record id</dt>
                <dd className={styles.monoValue}>{parcel.id}</dd>
              </dl>
            </details>
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
            className={styles.modalDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="lease-interest-submit-success-title"
            onClick={(e) => e.stopPropagation()}
          >
            <h2
              id="lease-interest-submit-success-title"
              className={styles.successTitle}
            >
              Submitted for preliminary verification
            </h2>
            <p className={styles.successBody}>
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

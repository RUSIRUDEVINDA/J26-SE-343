"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import {
  getLeaseInterestDocumentChecklist,
  getOrCreateLeaseInterestDraft,
  saveLeaseInterestDraft,
  submitLeaseInterest,
} from "../services/citizenWorkspaceApi";
import { getParcelById } from "../services/landIntelligenceApi";
import type {
  LandParcelResponse,
  LeaseInterestDocumentChecklist,
  LeaseInterestRequestDetail,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { displayOrUnknown, formatArea } from "../utils/formatters";
import { formatSourceReportedLandUseLine } from "../utils/sourceReportedCadastre";
import styles from "./LeaseInterestFormView.module.css";

const STEPS = ["Applicant", "Proposal", "Documents", "Review"] as const;

function statusLabel(status: number): string {
  switch (status) {
    case 1:
      return "Draft";
    case 2:
      return "Submitted";
    case 3:
      return "Pending verification";
    case 4:
      return "Needs information";
    case 5:
      return "Declined";
    case 6:
      return "Referred to authority";
    default:
      return "Unknown";
  }
}

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

export function LeaseInterestFormView({ parcelId }: { parcelId: string }) {
  const [step, setStep] = useState(0);
  const [parcel, setParcel] = useState<LandParcelResponse | null>(null);
  const [request, setRequest] = useState<LeaseInterestRequestDetail | null>(
    null,
  );
  const [draft, setDraft] = useState<UpsertLeaseInterestDraft>(emptyDraft());
  const [checklist, setChecklist] =
    useState<LeaseInterestDocumentChecklist | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<string[]>([]);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const idempotencyKeyRef = useRef<string | null>(null);

  const readOnly = request != null && request.status !== 1;

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [parcelRes, draftRes, checklistRes] = await Promise.all([
        getParcelById(parcelId),
        getOrCreateLeaseInterestDraft(parcelId),
        getLeaseInterestDocumentChecklist(),
      ]);
      setParcel(parcelRes);
      setRequest(draftRes);
      setDraft({
        applicant: draftRes.applicant,
        proposal: draftRes.proposal,
        supporting: draftRes.supporting,
        consentToPreliminaryAssessment: draftRes.consentToPreliminaryAssessment,
      });
      setChecklist(checklistRes);
      setDirty(false);
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
  }, [parcelId]);

  useEffect(() => {
    void load();
  }, [load]);

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

  const landUseLine = useMemo(() => {
    if (!parcel?.cadastralSource) {
      return displayOrUnknown(
        parcel?.currentUse
          ? landUseTypeLabels[parcel.currentUse.type]
          : undefined,
      );
    }
    return formatSourceReportedLandUseLine(parcel.cadastralSource) || "Unavailable";
  }, [parcel]);

  if (loading) {
    return <LoadingState message="Loading lease interest form…" />;
  }

  if (error && !parcel) {
    return <ErrorMessage message={error} />;
  }

  return (
    <div className={styles.layout}>
      <section className={styles.panel} aria-label="Lease interest form">
        <p className={styles.notice} role="note">
          <strong>Availability not verified.</strong> Expressing lease interest
          records your request for preliminary assessment. It does not reserve
          land, create a lease, or establish payment obligations.
        </p>

        {readOnly && request ? (
          <p className={styles.notice} role="status">
            Reference:{" "}
            <strong>{request.acknowledgementReference ?? "—"}</strong> · Status:{" "}
            <strong>{statusLabel(request.status)}</strong>
          </p>
        ) : null}

        <ol className={styles.steps} aria-label="Form steps">
          {STEPS.map((label, index) => (
            <li key={label}>
              <button
                type="button"
                className={`${styles.step} ${step === index ? styles.stepActive : ""}`}
                onClick={() => setStep(index)}
              >
                {index + 1}. {label}
              </button>
            </li>
          ))}
        </ol>

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
              <label htmlFor="legal-name">Legal name</label>
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
                className={styles.input}
                disabled={readOnly}
                placeholder="e.g. 2027-Q2"
                value={draft.proposal.desiredStartPeriod}
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
              <label htmlFor="area">Requested area (ha, optional)</label>
              <input
                id="area"
                type="number"
                min={0}
                step="0.01"
                className={styles.input}
                disabled={readOnly}
                value={draft.proposal.requestedAreaHectares ?? ""}
                onChange={(e) =>
                  patchDraft({
                    proposal: {
                      ...draft.proposal,
                      requestedAreaHectares:
                        e.target.value === "" ? null : Number(e.target.value),
                    },
                  })
                }
              />
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

        {step === 3 && request ? (
          <div>
            <h3 style={{ marginTop: 0 }}>Review</h3>
            <ul className={styles.metaList}>
              <li>
                Parcel: {displayOrUnknown(parcel?.identifier.cadastralNumber)} (
                {parcelId})
              </li>
              <li>
                Applicant: {draft.applicant.legalName} ·{" "}
                {draft.applicant.contactEmail}
              </li>
              <li>
                Purpose:{" "}
                {landUseTypeLabels[draft.proposal.requestedPurpose] ?? "Unknown"}{" "}
                · {draft.proposal.requestedDurationYears} years requested (not
                approved term)
              </li>
            </ul>
            <label className={styles.field} style={{ display: "flex", gap: 8 }}>
              <input
                type="checkbox"
                disabled={readOnly}
                checked={draft.consentToPreliminaryAssessment}
                onChange={(e) =>
                  patchDraft({ consentToPreliminaryAssessment: e.target.checked })
                }
              />
              I consent to submit this information for preliminary assessment.
            </label>
            <h4>Cost summary (informational)</h4>
            <p style={{ fontSize: "0.8rem" }}>{request.costSummary.disclaimer}</p>
            <table className={styles.costTable}>
              <thead>
                <tr>
                  <th>Line</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {request.costSummary.lines.map((line) => (
                  <tr key={line.code}>
                    <td>{line.label}</td>
                    <td>
                      {line.status === "PendingAuthorityAssessment"
                        ? "Pending authority assessment"
                        : line.status}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}

        {!readOnly ? (
          <div className={styles.actions}>
            {step > 0 ? (
              <Button type="button" variant="secondary" onClick={() => setStep(step - 1)}>
                Back
              </Button>
            ) : null}
            {step < 3 ? (
              <Button type="button" variant="primary" onClick={() => setStep(step + 1)}>
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
            {step === 3 ? (
              <Button
                type="button"
                variant="primary"
                onClick={() => void handleSubmit()}
                disabled={submitting}
              >
                {submitting ? "Submitting…" : "Submit for verification"}
              </Button>
            ) : null}
          </div>
        ) : (
          <div className={styles.actions}>
            <Button href="/land-intelligence/my-requests" variant="primary">
              My requests
            </Button>
          </div>
        )}
      </section>

      <aside className={styles.panel} aria-label="Parcel summary">
        {parcel ? (
          <>
            <h3 style={{ marginTop: 0 }}>Parcel summary</h3>
            <ul className={styles.metaList}>
              <li>
                Cadastral: {displayOrUnknown(parcel.identifier.cadastralNumber)}
              </li>
              <li>District: {displayOrUnknown(parcel.location.district)}</li>
              <li>Area: {formatArea(parcel.area.value, parcel.area.unit)}</li>
              <li>Source land use: {landUseLine}</li>
              <li>
                Lease availability:{" "}
                {parcel.availability?.actualLeaseAvailabilityLabel ??
                  "Not verified"}
              </li>
            </ul>
            <div className={styles.actions}>
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
    </div>
  );
}

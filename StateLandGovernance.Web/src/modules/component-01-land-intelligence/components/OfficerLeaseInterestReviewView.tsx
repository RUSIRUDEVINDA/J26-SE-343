"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { officerLeaseInterestQueueNavItem } from "@/shared/constants/navigation";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { getOfficerLeaseInterestRequest, listOfficerValuations, recordOfficerValuation, updateOfficerLeaseInterestStatus } from "../services/officerLeaseInterestApi";
import type {
  LeaseInterestOfficerRequestDetail,
  LeaseInterestRequestStatus,
  LeaseInterestValuation,
  LeaseInterestValuationFormType,
  RecordLeaseInterestValuation,
} from "../types/landIntelligence";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { useModalDialogA11y } from "../hooks/useModalDialogA11y";
import { landUseTypeLabels } from "../utils/enumMappings";
import {
  officerValuationPanelMode,
  selectReadOnlyValuation,
  unavailableValuationMessage,
} from "../utils/officerClosedCase";
import {
  leaseInterestApplicantOutcome,
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "../utils/leaseInterestStatus";
import { AuthorityValuationWorksheet } from "./AuthorityValuationWorksheet";
import {
  beginValuationSaveAttempt,
  markValuationSaveFailed,
  markValuationSaveSucceeded,
  preserveValuationFormOnFailure,
  type ValuationSaveAttemptState,
} from "../utils/officerValuationSave";
import styles from "./OfficerLeaseInterestReviewView.module.css";

const REVIEW_STATUSES: LeaseInterestRequestStatus[] = [4, 5, 6];
const OFFICER_QUEUE_HREF = officerLeaseInterestQueueNavItem.href;
const OFFICER_DECISION_NAVIGATE_STATUSES: LeaseInterestRequestStatus[] = [5, 6];
const formatDate = (value: string) => new Date(value).toLocaleString("en-LK", { dateStyle: "medium", timeStyle: "short" });
type ValuationForm = {
  formType: LeaseInterestValuationFormType;
  assessedLandValueLkr: string;
  leaseFileNumber: string;
  divisionalSecretariat: string;
  leaseApprovalDate: string;
  leaseCommencementDate: string;
  valuationReportDate: string;
  leaseAgreementDate: string;
  previousAssessedValueLkr: string;
  leaseRatePercent: string;
  annualLeaseAmountLkr: string;
  firstPeriodAmountLkr: string;
  secondPeriodAmountLkr: string;
  secondPeriodAnnualAmountLkr: string;
  circular2018AmountLkr: string;
  circular2020SixAdjustmentLkr: string;
  firstPeriodLines: { period: string; amountLkr: string }[];
  secondPeriodLines: { period: string; amountLkr: string }[];
  worksheetNotes: string;
};
const emptyValuation: ValuationForm = {
  formType: "AgriculturalCommercial", assessedLandValueLkr: "", leaseFileNumber: "",
  divisionalSecretariat: "", leaseApprovalDate: "", leaseCommencementDate: "",
  valuationReportDate: "", leaseAgreementDate: "", previousAssessedValueLkr: "",
  leaseRatePercent: "", annualLeaseAmountLkr: "", firstPeriodAmountLkr: "",
  secondPeriodAmountLkr: "", secondPeriodAnnualAmountLkr: "", circular2018AmountLkr: "",
  circular2020SixAdjustmentLkr: "", firstPeriodLines: [{ period: "", amountLkr: "" }],
  secondPeriodLines: [{ period: "", amountLkr: "" }], worksheetNotes: "",
};
const typeForPurpose = (purpose: number): LeaseInterestValuationFormType =>
  purpose === 4 ? "Residential" : "AgriculturalCommercial";
const fromValuation = (value: LeaseInterestValuation): ValuationForm => {
  const worksheet = value.worksheet;
  const text = (input: string | number | null | undefined) => input == null ? "" : String(input);
  const lines = (rows: { period: string; amountLkr?: number | null }[] | null | undefined) =>
    rows?.length ? rows.map((row) => ({ period: row.period, amountLkr: text(row.amountLkr) })) : [{ period: "", amountLkr: "" }];
  return {
    formType: value.formType === "Residential" ? "Residential" : "AgriculturalCommercial",
    assessedLandValueLkr: text(value.assessedLandValueLkr),
    leaseFileNumber: text(worksheet?.leaseFileNumber), divisionalSecretariat: text(worksheet?.divisionalSecretariat),
    leaseApprovalDate: text(worksheet?.leaseApprovalDate), leaseCommencementDate: text(worksheet?.leaseCommencementDate),
    valuationReportDate: text(worksheet?.valuationReportDate), leaseAgreementDate: text(worksheet?.leaseAgreementDate),
    previousAssessedValueLkr: text(worksheet?.previousAssessedValueLkr), leaseRatePercent: text(worksheet?.leaseRatePercent),
    annualLeaseAmountLkr: text(worksheet?.annualLeaseAmountLkr), firstPeriodAmountLkr: text(worksheet?.firstPeriodAmountLkr),
    secondPeriodAmountLkr: text(worksheet?.secondPeriodAmountLkr), secondPeriodAnnualAmountLkr: text(worksheet?.secondPeriodAnnualAmountLkr),
    circular2018AmountLkr: text(worksheet?.circular2018AmountLkr),
    circular2020SixAdjustmentLkr: text(worksheet?.circular2020SixAdjustmentLkr),
    firstPeriodLines: lines(worksheet?.firstPeriodLines), secondPeriodLines: lines(worksheet?.secondPeriodLines),
    worksheetNotes: text(worksheet?.worksheetNotes),
  };
};

export function OfficerLeaseInterestReviewView({ requestId }: { requestId: string }) {
  const router = useRouter();
  const [detail, setDetail] = useState<LeaseInterestOfficerRequestDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [note, setNote] = useState("");
  const [newStatus, setNewStatus] = useState<LeaseInterestRequestStatus>(4);
  const [saving, setSaving] = useState(false);
  const [valuations, setValuations] = useState<LeaseInterestValuation[]>([]);
  const [valuationForm, setValuationForm] = useState<ValuationForm>(emptyValuation);
  const [savingValuation, setSavingValuation] = useState(false);
  const [valuationError, setValuationError] = useState<string | null>(null);
  const [readOnlyRevisionId, setReadOnlyRevisionId] = useState<string | null>(null);
  const [valuationSuccessOpen, setValuationSuccessOpen] = useState(false);
  const [valuationSuccessRevision, setValuationSuccessRevision] = useState<number | null>(null);
  const [decisionSuccessOpen, setDecisionSuccessOpen] = useState(false);
  const [decisionSuccessStatus, setDecisionSuccessStatus] =
    useState<LeaseInterestRequestStatus | null>(null);
  const valuationSaveAttemptRef = useRef<ValuationSaveAttemptState>({
    idempotencyKey: null,
    inFlight: false,
  });

  function updateValuation<K extends keyof ValuationForm>(key: K, value: ValuationForm[K]) {
    setValuationForm((current) => ({ ...current, [key]: value }));
  }

  function updatePeriodLine(group: "firstPeriodLines" | "secondPeriodLines", index: number, key: "period" | "amountLkr", value: string) {
    setValuationForm((current) => ({
      ...current,
      [group]: current[group].map((row, rowIndex) => rowIndex === index ? { ...row, [key]: value } : row),
    }));
  }

  function addPeriodLine(group: "firstPeriodLines" | "secondPeriodLines") {
    setValuationForm((current) => ({ ...current, [group]: [...current[group], { period: "", amountLkr: "" }] }));
  }

  function removePeriodLine(group: "firstPeriodLines" | "secondPeriodLines", index: number) {
    setValuationForm((current) => ({ ...current, [group]: current[group].filter((_, rowIndex) => rowIndex !== index) }));
  }

  async function refreshValuations() {
    const recorded = await listOfficerValuations(requestId);
    setValuations(recorded);
    if (recorded[0]) {
      setValuationForm(fromValuation(recorded[0]));
    }
  }

  async function closeValuationSuccessPopup() {
    setValuationSuccessOpen(false);
    setValuationSuccessRevision(null);
    try {
      await refreshValuations();
      const result = await getOfficerLeaseInterestRequest(requestId);
      setDetail(result);
    } catch {
      // Keep the successful save visible even if the follow-up refresh fails.
    }
  }

  async function handleValuationSave() {
    if (!detail) return;
    const attempt = beginValuationSaveAttempt(valuationSaveAttemptRef.current);
    if (!attempt.ok) {
      return;
    }
    valuationSaveAttemptRef.current = attempt.nextState;
    setSavingValuation(true);
    setValuationError(null);
    setSuccess(null);
    const formSnapshot = preserveValuationFormOnFailure(valuationForm);
    if (detail.status !== 6) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      setSavingValuation(false);
      setValuationError(
        "Record the valuation only after the request has been referred to the authority.",
      );
      return;
    }
    if (!formSnapshot.leaseFileNumber.trim()) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      setSavingValuation(false);
      setValuationError("Enter the lease file number.");
      return;
    }
    if (!formSnapshot.divisionalSecretariat.trim()) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      setSavingValuation(false);
      setValuationError("Enter the Divisional Secretariat division.");
      return;
    }
    const amount = Number(formSnapshot.assessedLandValueLkr);
    if (!Number.isFinite(amount) || amount <= 0) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      setSavingValuation(false);
      setValuationError("Enter a positive officer-assessed land value.");
      return;
    }
    const optionalNumber = (value: string): number | undefined => {
      const trimmed = value.trim();
      if (!trimmed) {
        return undefined;
      }
      const parsed = Number(trimmed);
      return Number.isFinite(parsed) ? parsed : Number.NaN;
    };
    const optionalDate = (value: string): string | undefined => {
      const trimmed = value.trim();
      return trimmed || undefined;
    };
    const enteredFigures = [
      formSnapshot.previousAssessedValueLkr, formSnapshot.leaseRatePercent,
      formSnapshot.annualLeaseAmountLkr, formSnapshot.firstPeriodAmountLkr,
      formSnapshot.secondPeriodAmountLkr,
      ...(formSnapshot.formType === "Residential" ? [formSnapshot.secondPeriodAnnualAmountLkr] :
        [formSnapshot.circular2018AmountLkr, formSnapshot.circular2020SixAdjustmentLkr]),
    ];
    if (enteredFigures.some((value) => value.trim() && (!Number.isFinite(Number(value)) || Number(value) < 0))) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      setSavingValuation(false);
      setValuationError("Enter valid non-negative numbers for all manually entered figures.");
      return;
    }
    const firstPeriodLines = formSnapshot.firstPeriodLines
      .filter((row) => row.period.trim() || row.amountLkr.trim())
      .map((row) => ({
        period: row.period.trim(),
        amountLkr: optionalNumber(row.amountLkr),
      }));
    const secondPeriodLines = formSnapshot.secondPeriodLines
      .filter((row) => row.period.trim() || row.amountLkr.trim())
      .map((row) => ({
        period: row.period.trim(),
        amountLkr: optionalNumber(row.amountLkr),
      }));
    if (
      [...firstPeriodLines, ...secondPeriodLines].some(
        (row) =>
          !row.period ||
          (row.amountLkr !== undefined &&
            (!Number.isFinite(row.amountLkr) || row.amountLkr < 0)),
      )
    ) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      setSavingValuation(false);
      setValuationError("Complete the period description and enter a non-negative amount for each table row.");
      return;
    }
    try {
      const worksheet: RecordLeaseInterestValuation["worksheet"] = {
        leaseFileNumber: formSnapshot.leaseFileNumber.trim(),
        divisionalSecretariat: formSnapshot.divisionalSecretariat.trim(),
        firstPeriodLines,
        secondPeriodLines,
      };
      const leaseApprovalDate = optionalDate(formSnapshot.leaseApprovalDate);
      const leaseCommencementDate = optionalDate(formSnapshot.leaseCommencementDate);
      const valuationReportDate = optionalDate(formSnapshot.valuationReportDate);
      const leaseAgreementDate = optionalDate(formSnapshot.leaseAgreementDate);
      const previousAssessedValueLkr = optionalNumber(formSnapshot.previousAssessedValueLkr);
      const leaseRatePercent = optionalNumber(formSnapshot.leaseRatePercent);
      const annualLeaseAmountLkr = optionalNumber(formSnapshot.annualLeaseAmountLkr);
      const firstPeriodAmountLkr = optionalNumber(formSnapshot.firstPeriodAmountLkr);
      const secondPeriodAmountLkr = optionalNumber(formSnapshot.secondPeriodAmountLkr);
      const secondPeriodAnnualAmountLkr =
        formSnapshot.formType === "Residential"
          ? optionalNumber(formSnapshot.secondPeriodAnnualAmountLkr)
          : undefined;
      const circular2018AmountLkr =
        formSnapshot.formType === "AgriculturalCommercial"
          ? optionalNumber(formSnapshot.circular2018AmountLkr)
          : undefined;
      const circular2020SixAdjustmentLkr =
        formSnapshot.formType === "AgriculturalCommercial"
          ? optionalNumber(formSnapshot.circular2020SixAdjustmentLkr)
          : undefined;
      const worksheetNotes = formSnapshot.worksheetNotes.trim() || undefined;

      if (leaseApprovalDate) worksheet.leaseApprovalDate = leaseApprovalDate;
      if (leaseCommencementDate) worksheet.leaseCommencementDate = leaseCommencementDate;
      if (valuationReportDate) worksheet.valuationReportDate = valuationReportDate;
      if (leaseAgreementDate) worksheet.leaseAgreementDate = leaseAgreementDate;
      if (previousAssessedValueLkr !== undefined) worksheet.previousAssessedValueLkr = previousAssessedValueLkr;
      if (leaseRatePercent !== undefined) worksheet.leaseRatePercent = leaseRatePercent;
      if (annualLeaseAmountLkr !== undefined) worksheet.annualLeaseAmountLkr = annualLeaseAmountLkr;
      if (firstPeriodAmountLkr !== undefined) worksheet.firstPeriodAmountLkr = firstPeriodAmountLkr;
      if (secondPeriodAmountLkr !== undefined) worksheet.secondPeriodAmountLkr = secondPeriodAmountLkr;
      if (secondPeriodAnnualAmountLkr !== undefined) {
        worksheet.secondPeriodAnnualAmountLkr = secondPeriodAnnualAmountLkr;
      }
      if (circular2018AmountLkr !== undefined) worksheet.circular2018AmountLkr = circular2018AmountLkr;
      if (circular2020SixAdjustmentLkr !== undefined) {
        worksheet.circular2020SixAdjustmentLkr = circular2020SixAdjustmentLkr;
      }
      if (worksheetNotes) worksheet.worksheetNotes = worksheetNotes;

      const recorded = await recordOfficerValuation(
        requestId,
        {
          formType: formSnapshot.formType,
          assessedLandValueLkr: amount,
          worksheet,
        },
        attempt.idempotencyKey,
      );
      valuationSaveAttemptRef.current = markValuationSaveSucceeded();
      setValuations((current) => {
        const withoutDuplicate = current.filter((item) => item.id !== recorded.id);
        return [recorded, ...withoutDuplicate];
      });
      setSuccess(null);
      setValuationSuccessRevision(recorded.revision);
      setValuationSuccessOpen(true);
      // Refresh history from the server after the database write succeeds.
      void refreshValuations().catch(() => undefined);
    } catch (err) {
      valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      // Preserve every entered field; do not reset the worksheet on failure.
      setValuationForm(preserveValuationFormOnFailure(formSnapshot));
      setValuationSuccessOpen(false);
      setValuationSuccessRevision(null);
      setValuationError(
        err instanceof ApiClientError
          ? err.errors?.join(" ") || err.detail || err.title
          : "Unable to record valuation.",
      );
    } finally {
      setSavingValuation(false);
      if (valuationSaveAttemptRef.current.inFlight) {
        valuationSaveAttemptRef.current = markValuationSaveFailed(valuationSaveAttemptRef.current);
      }
    }
  }

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const [result, recorded] = await Promise.all([
          getOfficerLeaseInterestRequest(requestId),
          listOfficerValuations(requestId),
        ]);
        if (cancelled) {
          return;
        }
        setDetail(result);
        setValuations(recorded);
        setValuationForm(
          recorded[0]
            ? fromValuation(recorded[0])
            : {
                ...emptyValuation,
                formType: typeForPurpose(result.proposal.requestedPurpose),
              },
        );
        setNewStatus(4);
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof ApiClientError
              ? err.detail || err.title
              : "Unable to load request for review.",
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [requestId]);

  const { dialogRef: valuationSuccessDialogRef } = useModalDialogA11y({
    open: valuationSuccessOpen,
    onClose: () => {
      void closeValuationSuccessPopup();
    },
  });

  const { dialogRef: decisionSuccessDialogRef } = useModalDialogA11y({
    open: decisionSuccessOpen,
    onClose: () => {
      closeDecisionSuccessPopup();
    },
  });

  function closeDecisionSuccessPopup() {
    setDecisionSuccessOpen(false);
    setDecisionSuccessStatus(null);
    router.push(OFFICER_QUEUE_HREF);
  }

  async function handleStatusUpdate() {
    if (!detail || saving || !note.trim()) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const updated = await updateOfficerLeaseInterestStatus(requestId, {
        newStatus, note: note.trim(), visibleToApplicant: true,
      });
      setDetail(updated);
      setNote("");
      if (OFFICER_DECISION_NAVIGATE_STATUSES.includes(updated.status)) {
        setSuccess(null);
        setDecisionSuccessStatus(updated.status);
        setDecisionSuccessOpen(true);
      } else {
        setSuccess(`Decision recorded: ${leaseInterestStatusLabel(updated.status)}.`);
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.errors?.join(" ") || err.detail || err.title : "Status update failed.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingState message="Loading request for officer review…" />;
  if (error && !detail) return <ErrorMessage message={error} />;
  if (!detail) return null;

  const panelMode = officerValuationPanelMode(detail.status, valuations.length);
  const readOnlyValuation = selectReadOnlyValuation(valuations, readOnlyRevisionId);
  const valuationFieldA11y = valuationError
    ? ({ "aria-invalid": true, "aria-describedby": "valuation-error" } as const)
    : {};
  const canReview = detail.status === 3 || detail.status === 4;
  const outcomeOptions = detail.status === 4 ? [...REVIEW_STATUSES, 3 as const] : REVIEW_STATUSES;

  return (
    <div className={styles.page}>
      {decisionSuccessOpen && decisionSuccessStatus != null ? (
        <div
          className={styles.modalOverlay}
          role="presentation"
          onClick={() => {
            closeDecisionSuccessPopup();
          }}
        >
          <div
            ref={decisionSuccessDialogRef}
            className={styles.modalDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="officer-decision-success-title"
            aria-describedby="officer-decision-success-body"
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id="officer-decision-success-title" className={styles.modalTitle}>
              Decision recorded
            </h2>
            <p id="officer-decision-success-body" className={styles.modalBody}>
              This request is now{" "}
              <strong>{leaseInterestStatusLabel(decisionSuccessStatus)}</strong>.
              {" "}
              {leaseInterestApplicantOutcome(decisionSuccessStatus)}
              {" "}
              You will return to the verification queue.
            </p>
            <div className={styles.modalActions}>
              <Button
                type="button"
                variant="primary"
                onClick={() => {
                  closeDecisionSuccessPopup();
                }}
              >
                OK
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {valuationSuccessOpen && valuationSuccessRevision != null ? (
        <div
          className={styles.modalOverlay}
          role="presentation"
          onClick={() => { void closeValuationSuccessPopup(); }}
        >
          <div
            ref={valuationSuccessDialogRef}
            className={styles.modalDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="valuation-success-title"
            aria-describedby="valuation-success-body"
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id="valuation-success-title" className={styles.modalTitle}>
              Officer valuation recorded successfully
            </h2>
            <p id="valuation-success-body" className={styles.modalBody}>
              Valuation revision <strong>{valuationSuccessRevision}</strong> has
              been saved. The request status remains{" "}
              <strong>{leaseInterestStatusLabel(detail.status)}</strong>. This
              does not reserve land or approve a lease.
            </p>
            <div className={styles.modalActions}>
              <Button
                type="button"
                variant="primary"
                onClick={() => { void closeValuationSuccessPopup(); }}
              >
                Close
              </Button>
            </div>
          </div>
        </div>
      ) : null}
      <Link href="/land-intelligence/officer/lease-interest-lookup" className={styles.backLink}>← Back to applicant lookup</Link>
      <header className={styles.caseHeader}>
        <div>
          <p className={styles.eyebrow}>Lease interest request</p>
          <h2 className={styles.reference}>{detail.acknowledgementReference ?? "Lease interest request"}</h2>
          {detail.applicantOutcomeSummary.trim() ? (
            <p className={styles.headerText}>{detail.applicantOutcomeSummary}</p>
          ) : null}
        </div>
        <StatusBadge tone={leaseInterestStatusBadgeTone(detail.status)}>{leaseInterestStatusLabel(detail.status)}</StatusBadge>
      </header>
     
      {error ? <ErrorMessage id="officer-review-error" message={error} /> : null}
      {success ? <p className={styles.success} role="status">{success}</p> : null}
      <div className={styles.contentGrid}>
        <div className={styles.mainColumn}>
          <section className={styles.card} aria-labelledby="review-details-title">
            <div className={styles.sectionHeading}>
              <div><p className={styles.eyebrow}>Case details</p><h3 id="review-details-title">Applicant and parcel</h3></div>
              <Link href={`/land-intelligence/parcels/${detail.landParcelId}`} className={styles.textLink}>Open parcel intelligence →</Link>
            </div>
            <dl className={styles.detailsGrid}>
              <div><dt>Applicant</dt><dd>{detail.applicant.legalName}</dd></div>
              <div><dt>Parcel number</dt><dd>{detail.cadastralNumber ?? "Not recorded"}</dd></div>
              <div><dt>District</dt><dd>{detail.district ?? "Not recorded"}</dd></div>
              <div><dt>Requested purpose</dt><dd>{landUseTypeLabels[detail.proposal.requestedPurpose] ?? "Unknown"}</dd></div>
              <div><dt>Requested term</dt><dd>{detail.proposal.requestedDurationYears} years <span className={styles.secondary}>· not approved</span></dd></div>
              <div><dt>Submitted</dt><dd>{detail.submittedAtUtc ? formatDate(detail.submittedAtUtc) : "Not submitted"}</dd></div>
            </dl>
          </section>
          <section className={styles.card} aria-labelledby="valuation-title">
            <div className={styles.sectionHeading}>
              <div><p className={styles.eyebrow}>Officer assessment</p><h3 id="valuation-title">Land valuation</h3></div>
            </div>
            {panelMode === "editable" ? (
              <p className={styles.fieldHelp}>Copy the officer-confirmed entries from the applicable paper worksheet. All rates and amounts below are entered manually; the system does not calculate them. Each save creates a new revision.</p>
            ) : (
              <p className={styles.fieldHelp}>
                This worksheet is read-only because the request is{" "}
                <strong>{leaseInterestStatusLabel(detail.status).toLowerCase()}</strong>.
                Values are shown exactly as recorded by the officer; the system does not calculate lease rates or totals.
              </p>
            )}
            {valuations[0] ? (
              <p className={styles.valuationLatest}>
                Latest recorded value: <strong>LKR {valuations[0].assessedLandValueLkr.toLocaleString("en-LK", { minimumFractionDigits: 2 })}</strong>
                {` · revision ${valuations[0].revision} · ${valuations[0].worksheet?.leaseFileNumber ?? valuations[0].valuationReference ?? "No file number"}`}
              </p>
            ) : <p className={styles.valuationPending}>Valuation pending officer inspection.</p>}
            {valuationError ? <ErrorMessage id="valuation-error" message={valuationError} /> : null}
            {panelMode === "unavailable" ? <p className={styles.valuationPending}>{unavailableValuationMessage(detail.status)}</p> : null}
            {panelMode === "readonly" && readOnlyValuation ? (
              <div className={styles.readOnlyWorksheet}>
                <div className={styles.field}>
                  <label htmlFor="officer-valuation-revision">Valuation revision to inspect</label>
                  <select
                    id="officer-valuation-revision"
                    className={styles.input}
                    value={readOnlyValuation.id}
                    onChange={(event) => setReadOnlyRevisionId(event.target.value || null)}
                  >
                    {valuations.map((item) => (
                      <option key={item.id} value={item.id}>
                        Revision {item.revision}
                        {valuations[0]?.id === item.id ? " (latest)" : ""} · {formatDate(item.recordedAtUtc)}
                      </option>
                    ))}
                  </select>
                </div>
                <AuthorityValuationWorksheet valuation={readOnlyValuation} />
              </div>
            ) : null}
            {panelMode === "editable" ? <form className={styles.valuationForm} aria-describedby={valuationError ? "valuation-error" : undefined} onSubmit={(event) => { event.preventDefault(); void handleValuationSave(); }}>
              <div className={styles.valuationGrid}>
                <div className={styles.field}><label htmlFor="valuation-type">Paper form type *</label><select id="valuation-type" className={styles.input} value={valuationForm.formType} onChange={(event) => updateValuation("formType", event.target.value as LeaseInterestValuationFormType)}><option value="AgriculturalCommercial">Agricultural / commercial lease</option><option value="Residential">Residential lease</option></select></div>
                <div className={styles.field}><label htmlFor="lease-file-number">Lease file number *</label><input id="lease-file-number" className={styles.input} maxLength={128} required {...valuationFieldA11y} value={valuationForm.leaseFileNumber} onChange={(event) => updateValuation("leaseFileNumber", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="divisional-secretariat">Divisional Secretariat division *</label><input id="divisional-secretariat" className={styles.input} maxLength={200} required {...valuationFieldA11y} value={valuationForm.divisionalSecretariat} onChange={(event) => updateValuation("divisionalSecretariat", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="lease-approval-date">Lease approval date</label><input id="lease-approval-date" className={styles.input} type="date" value={valuationForm.leaseApprovalDate} onChange={(event) => updateValuation("leaseApprovalDate", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="lease-commencement-date">Lease commencement date</label><input id="lease-commencement-date" className={styles.input} type="date" value={valuationForm.leaseCommencementDate} onChange={(event) => updateValuation("leaseCommencementDate", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="valuation-report-date">Valuation report issued date</label><input id="valuation-report-date" className={styles.input} type="date" value={valuationForm.valuationReportDate} onChange={(event) => updateValuation("valuationReportDate", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="lease-agreement-date">Lease agreement date</label><input id="lease-agreement-date" className={styles.input} type="date" value={valuationForm.leaseAgreementDate} onChange={(event) => updateValuation("leaseAgreementDate", event.target.value)} /></div>
              </div>
              <h4 className={styles.worksheetHeading}>Valuation and lease figures — officer entry</h4>
              <div className={styles.valuationGrid}>
                <div className={styles.field}><label htmlFor="assessed-value">Assessed land value (LKR) *</label><input id="assessed-value" className={styles.input} type="number" min="0.01" step="0.01" required {...valuationFieldA11y} value={valuationForm.assessedLandValueLkr} onChange={(event) => updateValuation("assessedLandValueLkr", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="previous-value">Previous assessed value (LKR)</label><input id="previous-value" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.previousAssessedValueLkr} onChange={(event) => updateValuation("previousAssessedValueLkr", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="lease-percentage">Lease percentage (%) — manual</label><input id="lease-percentage" className={styles.input} type="number" min="0" max="100" step="0.0001" value={valuationForm.leaseRatePercent} onChange={(event) => updateValuation("leaseRatePercent", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="annual-lease-amount">Annual lease amount (LKR) — manual</label><input id="annual-lease-amount" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.annualLeaseAmountLkr} onChange={(event) => updateValuation("annualLeaseAmountLkr", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="first-period-amount">First lease period amount (LKR) — manual</label><input id="first-period-amount" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.firstPeriodAmountLkr} onChange={(event) => updateValuation("firstPeriodAmountLkr", event.target.value)} /></div>
                <div className={styles.field}><label htmlFor="second-period-amount">Second lease period amount (LKR) — manual</label><input id="second-period-amount" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.secondPeriodAmountLkr} onChange={(event) => updateValuation("secondPeriodAmountLkr", event.target.value)} /></div>
                {valuationForm.formType === "Residential" ? <div className={styles.field}><label htmlFor="second-period-annual">Second period annual amount (LKR) — manual</label><input id="second-period-annual" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.secondPeriodAnnualAmountLkr} onChange={(event) => updateValuation("secondPeriodAnnualAmountLkr", event.target.value)} /></div> : null}
                {valuationForm.formType === "AgriculturalCommercial" ? <><div className={styles.field}><label htmlFor="circular-2018-amount">01/2018 circular amount (LKR) — manual</label><input id="circular-2018-amount" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.circular2018AmountLkr} onChange={(event) => updateValuation("circular2018AmountLkr", event.target.value)} /></div><div className={styles.field}><label htmlFor="circular-2020-adjustment">2020/6 circular adjustment (LKR) — manual</label><input id="circular-2020-adjustment" className={styles.input} type="number" min="0" step="0.01" value={valuationForm.circular2020SixAdjustmentLkr} onChange={(event) => updateValuation("circular2020SixAdjustmentLkr", event.target.value)} /></div></> : null}
              </div>
              {(["firstPeriodLines", "secondPeriodLines"] as const).map((group) => <section key={group} className={styles.worksheetTable} aria-label={group === "firstPeriodLines" ? "First lease period rows" : "Second lease period rows"}>
                <h4 className={styles.worksheetHeading}>{group === "firstPeriodLines" ? "First lease period entries" : "Second lease period entries"}</h4>
                <p className={styles.fieldHelp}>Enter each period and amount exactly as recorded by the officer. No total is calculated.</p>
                {valuationForm[group].map((row, index) => <div className={styles.worksheetRow} key={`${group}-${index}`}>
                  <div className={styles.field}><label htmlFor={`${group}-period-${index}`}>Year / period {index + 1}</label><input id={`${group}-period-${index}`} className={styles.input} maxLength={120} value={row.period} onChange={(event) => updatePeriodLine(group, index, "period", event.target.value)} /></div>
                  <div className={styles.field}><label htmlFor={`${group}-amount-${index}`}>Amount (LKR)</label><input id={`${group}-amount-${index}`} className={styles.input} type="number" min="0" step="0.01" value={row.amountLkr} onChange={(event) => updatePeriodLine(group, index, "amountLkr", event.target.value)} /></div>
                  <button className={styles.rowRemove} type="button" onClick={() => removePeriodLine(group, index)} aria-label={`Remove ${group === "firstPeriodLines" ? "first" : "second"} period row ${index + 1}`}>Remove</button>
                </div>)}
                <button className={styles.rowAdd} type="button" disabled={valuationForm[group].length >= 12} onClick={() => addPeriodLine(group)}>+ Add period row</button>
              </section>)}
              <div className={styles.field}><label htmlFor="worksheet-notes">Worksheet notes (optional)</label><textarea id="worksheet-notes" className={styles.input} rows={3} maxLength={4000} value={valuationForm.worksheetNotes} onChange={(event) => updateValuation("worksheetNotes", event.target.value)} /></div>
              <Button type="submit" variant="primary" disabled={savingValuation}>{savingValuation ? "Recording…" : valuations.length ? "Record revised valuation" : "Record valuation"}</Button>
            </form> : null}
            {panelMode === "editable" && valuations.length > 0 ? <details className={styles.revisionHistory}><summary>Valuation history ({valuations.length})</summary><ol>{valuations.map((item) => <li key={item.id}>Revision {item.revision}: LKR {item.assessedLandValueLkr.toLocaleString("en-LK")} · {item.valuationReference} · {formatDate(item.recordedAtUtc)}</li>)}</ol></details> : null}
          </section>
          {detail.latestAuthorityDecision ? (
            <section className={styles.card} aria-labelledby="authority-outcome-title">
              <div className={styles.sectionHeading}>
                <div>
                  <p className={styles.eyebrow}>Authority outcome</p>
                  <h3 id="authority-outcome-title">
                    {detail.latestAuthorityDecision.decisionLabel}
                  </h3>
                </div>
              </div>
              <p className={styles.valuationLatest} role="status">
                Recorded {formatDate(detail.latestAuthorityDecision.recordedAtUtc)}
              </p>
              <p className={styles.fieldHelp}>
                {detail.latestAuthorityDecision.applicantFacingReason}
              </p>
              {detail.latestAuthorityDecision.decision === 1 || detail.status === 7 ? (
                <p className={styles.notice}>
                  Formal lease processing is still required. This preliminary
                  approval does not create a lease or reserve land.
                </p>
              ) : null}
            </section>
          ) : null}
          <section className={styles.card} aria-labelledby="review-history-title">
            <div className={styles.sectionHeading}><div><p className={styles.eyebrow}>History</p><h3 id="review-history-title">Activity timeline</h3></div></div>
            <ol className={styles.timeline}>
              {detail.statusHistory.map((entry, index) => (
                <li key={`${entry.changedAtUtc}-${index}`} className={styles.timelineItem}>
                  <span className={styles.timelineDot} aria-hidden="true" />
                  <div className={styles.timelineContent}>
                    <div className={styles.timelineTop}><strong>{leaseInterestStatusLabel(entry.status)}</strong><time dateTime={entry.changedAtUtc}>{formatDate(entry.changedAtUtc)}</time></div>
                    {entry.note ? <p>{entry.note}</p> : null}
                    {entry.visibleToApplicant === false ? <span className={styles.internalTag}>Internal note</span> : null}
                  </div>
                </li>
              ))}
            </ol>
          </section>
        </div>
        <aside className={styles.decisionCard} aria-labelledby="review-decision-title">
          <p className={styles.eyebrow}>Officer action</p>
          <h3 id="review-decision-title">Record a decision</h3>
          {canReview ? (
            <form onSubmit={(event) => { event.preventDefault(); void handleStatusUpdate(); }}>
              <div className={styles.field}>
                <label htmlFor="officer-new-status">Outcome</label>
                <select id="officer-new-status" className={styles.input} value={newStatus} disabled={saving}
                  onChange={(event) => setNewStatus(Number(event.target.value) as LeaseInterestRequestStatus)}>
                  {outcomeOptions.map((status) => <option key={status} value={status}>{leaseInterestStatusLabel(status)}</option>)}
                </select>
                <p className={styles.fieldHelp}>{newStatus === 3 ? "Return this request to the verification queue." : leaseInterestApplicantOutcome(newStatus)}</p>
              </div>
              <div className={styles.field}>
                <label htmlFor="officer-note">Note to applicant <span aria-hidden="true">*</span></label>
                <textarea id="officer-note" className={styles.input} rows={5} value={note} required disabled={saving}
                  aria-describedby={error ? "officer-note-help officer-review-error" : "officer-note-help"}
                  onChange={(event) => setNote(event.target.value)} placeholder="Explain the decision and what happens next." />
                <p id="officer-note-help" className={styles.fieldHelp}>This note will appear in the applicant’s request timeline.</p>
              </div>
              <Button type="submit" variant="primary" className={styles.submitButton} disabled={saving || !note.trim()}>
                {saving ? "Saving decision…" : "Record decision"}
              </Button>
            </form>
          ) : (
            <p className={styles.closedMessage} role="status">This review is closed. No further officer decision is available for this request.</p>
          )}
        </aside>
      </div>
    </div>
  );
}

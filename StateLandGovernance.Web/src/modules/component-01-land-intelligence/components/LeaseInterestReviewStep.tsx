"use client";

import { Button } from "@/shared/components/Button";
import type {
  LandParcelResponse,
  LeaseInterestDocumentChecklist,
  LeaseInterestRequestDetail,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";
import { AreaUnit } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { displayOrUnknown, formatArea } from "../utils/formatters";
import {
  displayOptionalText,
  formatCostLineAmount,
  formatCostLineStatus,
  groupCostLines,
} from "../utils/leaseInterestReview";
import { maskNic } from "../utils/nic";
import {
  sourceReportedClaimantCategoryDisplay,
  sourceReportedLandUseDisplay,
} from "../utils/sourceReportedCadastre";
import styles from "./LeaseInterestFormView.module.css";

function reviewSectionId(title: string): string {
  return `review-${title.toLowerCase().replace(/\s+/g, "-")}`;
}

function ReviewCard({
  title,
  editLabel,
  onEdit,
  readOnly,
  children,
  hideEdit,
}: {
  title: string;
  editLabel?: string;
  onEdit?: () => void;
  readOnly: boolean;
  children: React.ReactNode;
  hideEdit?: boolean;
}) {
  const sectionId = reviewSectionId(title);
  return (
    <section className={styles.reviewCard} aria-labelledby={sectionId}>
      <div className={styles.reviewCardHeader}>
        <h3 id={sectionId} className={styles.reviewCardTitle}>
          {title}
        </h3>
        {!readOnly && !hideEdit && editLabel && onEdit ? (
          <Button
            type="button"
            variant="ghost"
            className={styles.editButton}
            onClick={onEdit}
          >
            {editLabel}
          </Button>
        ) : null}
      </div>
      {children}
    </section>
  );
}

function ReviewRow({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <>
      <dt>{label}</dt>
      <dd className={mono ? styles.monoValue : undefined}>{value}</dd>
    </>
  );
}

export function LeaseInterestReviewStep({
  parcel,
  parcelId,
  draft,
  request,
  checklist,
  readOnly,
  onEditApplicant,
  onEditProposal,
  onEditDocuments,
  onViewParcel,
  consentChecked,
  onConsentChange,
}: {
  parcel: LandParcelResponse;
  parcelId: string;
  draft: UpsertLeaseInterestDraft;
  request: LeaseInterestRequestDetail;
  checklist: LeaseInterestDocumentChecklist | null;
  readOnly: boolean;
  onEditApplicant: () => void;
  onEditProposal: () => void;
  onEditDocuments: () => void;
  onViewParcel: () => void;
  consentChecked: boolean;
  onConsentChange: (checked: boolean) => void;
}) {
  const landUse =
    sourceReportedLandUseDisplay(parcel.cadastralSource) ||
    (parcel.currentUse
      ? landUseTypeLabels[parcel.currentUse.type]
      : "Unavailable");
  const claimant = sourceReportedClaimantCategoryDisplay(parcel.cadastralSource);
  const costGroups = groupCostLines(request.costSummary.lines);
  const submitted = request.status !== 1;

  return (
    <div className={styles.reviewStack}>
      {submitted && request.acknowledgementReference ? (
        <div className={styles.successPanel} role="status">
          <h3 className={styles.successTitle}>Submitted for preliminary verification</h3>
          <p className={styles.successBody}>
            Your acknowledgement reference is{" "}
            <strong>{request.acknowledgementReference}</strong>. You can track
            progress from My requests.
          </p>
          <Button href="/land-intelligence/my-requests" variant="primary">
            View my requests
          </Button>
        </div>
      ) : null}

      <ReviewCard
        title="Selected parcel"
        editLabel="View parcel"
        onEdit={onViewParcel}
        readOnly={readOnly}
      >
        <dl className={styles.reviewDl}>
          <ReviewRow
            label="Cadastral number"
            value={displayOrUnknown(parcel.identifier.cadastralNumber)}
            mono
          />
          <ReviewRow
            label="District"
            value={displayOrUnknown(parcel.location.district)}
          />
          <ReviewRow
            label="Area"
            value={formatArea(parcel.area.value, parcel.area.unit)}
          />
          <ReviewRow label="Source-reported land use" value={landUse} />
          {claimant ? (
            <ReviewRow
              label="Source-reported claimant category"
              value={claimant}
            />
          ) : null}
          <ReviewRow
            label="Lease availability (project)"
            value={
              parcel.availability?.actualLeaseAvailabilityLabel ?? "Not verified"
            }
          />
          {parcel.availability?.projectLeaseReady?.designationNotice ? (
            <ReviewRow
              label="Project designation notice"
              value={parcel.availability.projectLeaseReady.designationNotice}
            />
          ) : null}
        </dl>
        <div className={styles.reviewCardFooter}>
          <Button
            href={`/land-intelligence/parcels/${parcelId}/map`}
            variant="secondary"
          >
            View boundary on map
          </Button>
        </div>
      </ReviewCard>

      <ReviewCard
        title="Applicant"
        editLabel="Edit"
        onEdit={onEditApplicant}
        readOnly={readOnly}
      >
        <dl className={styles.reviewDl}>
        <ReviewRow
          label="Applicant type"
          value={draft.applicant.kind === 2 ? "Organization" : "Individual"}
        />
        <ReviewRow label="Full name" value={displayOptionalText(draft.applicant.legalName)} />
        {draft.applicant.kind === 1 ? (
          <ReviewRow
            label="NIC"
            value={maskNic(draft.applicant.nationalIdentityCardNumber)}
          />
        ) : null}
        {draft.applicant.kind === 2 ? (
          <>
            <ReviewRow
              label="Organization name"
              value={displayOptionalText(draft.applicant.organizationName)}
            />
            <ReviewRow
              label="Registration number"
              value={displayOptionalText(
                draft.applicant.organizationRegistrationNumber,
              )}
              mono
            />
            <ReviewRow
              label="Representative NIC"
              value={maskNic(
                draft.applicant.representativeNationalIdentityCardNumber,
              )}
            />
          </>
        ) : null}
        <ReviewRow label="Contact phone" value={displayOptionalText(draft.applicant.contactPhone)} />
        <ReviewRow label="Email" value={displayOptionalText(draft.applicant.contactEmail)} />
        <ReviewRow
          label="Postal address"
          value={displayOptionalText(draft.applicant.postalAddress)}
        />
        </dl>
      </ReviewCard>

      <ReviewCard
        title="Requested lease terms"
        editLabel="Edit"
        onEdit={onEditProposal}
        readOnly={readOnly}
      >
        <dl className={styles.reviewDl}>
        <ReviewRow
          label="Requested purpose"
          value={
            landUseTypeLabels[draft.proposal.requestedPurpose] ?? "Unknown"
          }
        />
        <ReviewRow
          label="Requested term"
          value={`${draft.proposal.requestedDurationYears} years`}
        />
        <ReviewRow
          label="Desired start period"
          value={displayOptionalText(draft.proposal.desiredStartPeriod)}
        />
        <ReviewRow
          label="Requested area"
          value={
            draft.proposal.requestedAreaHectares != null &&
            draft.proposal.requestedAreaHectares > 0
              ? formatArea(
                  draft.proposal.requestedAreaHectares,
                  AreaUnit.Hectares,
                )
              : "—"
          }
        />
        <ReviewRow
          label="Planned activities"
          value={displayOptionalText(draft.proposal.plannedActivitiesDescription)}
        />
        <ReviewRow
          label="Project summary"
          value={displayOptionalText(draft.proposal.projectSummary)}
        />
        </dl>
      </ReviewCard>

      <ReviewCard
        title="Documents"
        editLabel="Edit"
        onEdit={onEditDocuments}
        readOnly={readOnly}
      >
        <dl className={styles.reviewDl}>
        <ReviewRow
          label="Checklist"
          value={
            checklist?.items.length
              ? `${checklist.items.length} item(s) — guidance only (no upload in this demo)`
              : "Checklist unavailable"
          }
        />
        <ReviewRow
          label="Supporting notes"
          value={displayOptionalText(draft.supporting.supportingNotes)}
        />
        {checklist?.storageNote ? (
          <ReviewRow label="Storage note" value={checklist.storageNote} />
        ) : null}
        {checklist?.items.map((item) => (
          <ReviewRow
            key={item.code}
            label={item.label}
            value={item.note}
          />
        ))}
        </dl>
      </ReviewCard>

      <section
        className={styles.costStatusPanel}
        aria-labelledby="cost-status-heading"
      >
        <h3 id="cost-status-heading" className={styles.costStatusTitle}>
          Costs pending authority assessment
        </h3>
        <p className={styles.costStatusLead}>
          No rent, tax, deposit, or payment amount has been assessed. This is not
          an invoice or payment obligation.
        </p>
        {request.costSummary.disclaimer ? (
          <p className={styles.costDisclaimer}>{request.costSummary.disclaimer}</p>
        ) : null}
        <details className={styles.costBreakdown}>
          <summary className={styles.costBreakdownSummary}>
            View cost breakdown
          </summary>
          <div className={styles.costBreakdownBody}>
            {costGroups.map((group) => (
              <div key={group.title} className={styles.costGroup}>
                <h4 className={styles.costGroupTitle}>{group.title}</h4>
                <ul className={styles.costLineList}>
                  {group.lines.map((line) => {
                    const amount = formatCostLineAmount(
                      line,
                      request.costSummary,
                    );
                    return (
                      <li key={line.code} className={styles.costLineItem}>
                        <span className={styles.costLineLabel}>{line.label}</span>
                        <span className={styles.costLineStatus}>
                          {amount ?? formatCostLineStatus(line.status)}
                        </span>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}
          </div>
        </details>
      </section>

      {!readOnly ? (
        <section
          className={styles.consentSection}
          aria-labelledby="consent-heading"
        >
          <h3 id="consent-heading" className={styles.consentTitle}>
            Consent
          </h3>
          <label className={styles.consentLabel}>
            <input
              type="checkbox"
              className={styles.consentCheckbox}
              checked={consentChecked}
              onChange={(e) => onConsentChange(e.target.checked)}
            />
            <span>
              I consent to submit this information for preliminary assessment. I
              understand this does not reserve land or create a lease.
            </span>
          </label>
        </section>
      ) : null}
    </div>
  );
}

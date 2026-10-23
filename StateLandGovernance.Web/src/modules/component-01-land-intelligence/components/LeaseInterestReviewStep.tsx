"use client";

import { Button } from "@/shared/components/Button";
import type {
  LandParcelResponse,
  LeaseInterestDocumentChecklist,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";
import { AreaUnit } from "../types/landIntelligence";
import { isSupportedLandUseType } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { displayOrUnknown, formatArea } from "../utils/formatters";
import { displayOptionalText } from "../utils/leaseInterestReview";
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
            className={styles.reviewCardAction}
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
  checklist,
  readOnly,
  onEditApplicant,
  onEditProposal,
  onEditDocuments,
  onViewParcel,
  consentChecked,
  consentError,
  onConsentChange,
}: {
  parcel: LandParcelResponse;
  parcelId: string;
  draft: UpsertLeaseInterestDraft;
  checklist: LeaseInterestDocumentChecklist | null;
  readOnly: boolean;
  onEditApplicant: () => void;
  onEditProposal: () => void;
  onEditDocuments: () => void;
  onViewParcel: () => void;
  consentChecked: boolean;
  consentError?: string;
  onConsentChange: (checked: boolean) => void;
}) {
  const landUse =
    sourceReportedLandUseDisplay(parcel.cadastralSource) ||
    (parcel.currentUse
      ? landUseTypeLabels[parcel.currentUse.type]
      : "Unavailable");
  const claimant = sourceReportedClaimantCategoryDisplay(parcel.cadastralSource);

  return (
    <div className={styles.reviewStack}>
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
        </dl>
        <div className={styles.reviewCardFooter}>
          <Button
            href={`/land-intelligence/parcels/${parcelId}/map`}
            variant="ghost"
            className={styles.reviewCardFooterAction}
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
            value={displayOptionalText(draft.applicant.nationalIdentityCardNumber)}
          />
        ) : null}
        {draft.applicant.kind === 2 ? (
          <>
            <ReviewRow
              label="Organization name"
              value={displayOptionalText(draft.applicant.organizationName)}
            />
            <ReviewRow
              label="Business registration number (BRN)"
              value={displayOptionalText(
                draft.applicant.organizationRegistrationNumber,
              )}
              mono
            />
            <ReviewRow
              label="Representative NIC (optional)"
              value={displayOptionalText(
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
            isSupportedLandUseType(draft.proposal.requestedPurpose)
              ? landUseTypeLabels[draft.proposal.requestedPurpose]
              : "—"
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
              aria-invalid={consentError ? true : undefined}
              aria-describedby={consentError ? "consent-error" : undefined}
              onChange={(e) => onConsentChange(e.target.checked)}
            />
            <span>
              I consent to submit this information for preliminary assessment. I
              understand this does not reserve land or create a lease.
            </span>
          </label>
          {consentError ? (
            <p id="consent-error" className={styles.fieldError} role="alert">
              {consentError}
            </p>
          ) : null}
        </section>
      ) : null}
    </div>
  );
}

import Link from "next/link";
import {
  LandCategoryType,
  type LandParcelResponse,
  type LandRelationshipResponse,
} from "../types/landIntelligence";
import {
  landCategoryTypeLabels,
  landUseTypeLabels,
} from "../utils/enumMappings";
import { displayOrUnavailable, displayOrUnknown, formatArea } from "../utils/formatters";
import {
  sourceReportedClaimantCategoryDisplay,
  sourceReportedLandUseDisplay,
  sourceReportedLandUseEnglishDisplay,
} from "../utils/sourceReportedCadastre";
import {
  formatGnDivisionAssignment,
  formatGnDivisionPrimaryDisplay,
} from "../utils/gnDivisionMappingDisplay";
import styles from "./ParcelDetails.module.css";

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <span className={styles.value}>{value}</span>
    </div>
  );
}

function readableType(value: string): string {
  const words = value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .trim();
  return words.charAt(0).toUpperCase() + words.slice(1).toLowerCase();
}

function landCategoryConnectionLabel(raw: string): string {
  const enumValue = LandCategoryType[raw as keyof typeof LandCategoryType];
  if (typeof enumValue === "number") {
    return landCategoryTypeLabels[enumValue as LandCategoryType] ?? raw;
  }
  return readableType(raw);
}

/** Graph "Has category" uses governance enum on the parcel row, not cadastre land-use codes. */
function parcelConnectionDescription(
  item: LandRelationshipResponse,
  parcel: LandParcelResponse,
): string {
  const raw = item.description?.trim() ?? "";
  if (item.relationshipType === "HAS_CATEGORY") {
    const categoryLabel = landCategoryConnectionLabel(raw);
    if (raw === "Other" && parcel.cadastralSource) {
      const cadastreLandUse = sourceReportedLandUseEnglishDisplay(
        parcel.cadastralSource,
      );
      if (cadastreLandUse) {
        return `cadastre land use: ${cadastreLandUse}`;
      }
    }
    return categoryLabel;
  }
  return raw || "No descriptive label recorded";
}

export function ParcelDetails({
  parcel,
  relationships,
  relationshipsError,
  relationshipsLoading,
}: {
  parcel: LandParcelResponse;
  relationships?: LandRelationshipResponse[] | null;
  relationshipsError?: string | null;
  relationshipsLoading?: boolean;
}) {
  const gnMapping = parcel.gnDivisionMapping;
  const gnNeedsReview =
    gnMapping?.reviewStatusDisplay === "Needs administrative review";
  const cadastralNumber = displayOrUnknown(parcel.identifier.cadastralNumber);
  const sourceLandUse = parcel.cadastralSource
    ? sourceReportedLandUseDisplay(parcel.cadastralSource) || "Unavailable"
    : parcel.currentUse
      ? landUseTypeLabels[parcel.currentUse.type] ??
        displayOrUnavailable(parcel.currentUse.description)
      : "Unavailable";
  const claimant = parcel.cadastralSource
    ? sourceReportedClaimantCategoryDisplay(parcel.cadastralSource) ||
      "Unavailable"
    : "Unavailable";
  const gnDisplay = gnMapping
    ? gnNeedsReview
      ? "Needs administrative review"
      : formatGnDivisionPrimaryDisplay(gnMapping)
    : displayOrUnavailable(parcel.location.gramaNiladhariDivision);

  return (
    <div className={styles.details}>
      <header className={styles.hero}>
        <div>
          <p className={styles.eyebrow}>PARCEL RECORD</p>
          <h2 className={styles.parcelNumber}>{cadastralNumber}</h2>
          <p className={styles.heroMeta}>
            {displayOrUnavailable(parcel.location.district)} ·{" "}
            {formatArea(parcel.area.value, parcel.area.unit)} · {sourceLandUse}
          </p>
        </div>
        <span className={styles.sourceBadge}>Source-reported claimant: {claimant}</span>
      </header>

      <div className={styles.sectionGrid}>
        <section className={styles.card} aria-labelledby="parcel-overview-title">
          <h3 id="parcel-overview-title" className={styles.sectionTitle}>Parcel overview</h3>
          <div className={styles.grid}>
            <DetailRow label="Area" value={formatArea(parcel.area.value, parcel.area.unit)} />
            <DetailRow label={parcel.cadastralSource ? "Source-reported land use" : "Current use"} value={sourceLandUse} />
            <DetailRow label="Source-reported claimant" value={claimant} />
            <DetailRow label="Survey plan reference" value={displayOrUnavailable(parcel.identifier.surveyPlanReference)} />
          </div>
        </section>

        <section className={styles.card} aria-labelledby="parcel-location-title">
          <h3 id="parcel-location-title" className={styles.sectionTitle}>Location</h3>
          <div className={styles.grid}>
            <DetailRow label="Province" value={displayOrUnavailable(parcel.location.province)} />
            <DetailRow label="District" value={displayOrUnavailable(parcel.location.district)} />
            <DetailRow label="Grama Niladhari division" value={gnDisplay} />
          </div>
          {gnMapping ? (
            <p className={styles.inlineNote}>
              {gnNeedsReview
                ? "The source code and map marker need administrative review. See the GN mapping details below."
                : ""}
            </p>
          ) : null}
        </section>
      </div>

      {gnMapping ? (
        <section className={styles.card} aria-labelledby="parcel-gn-title">
          <div className={styles.sectionHeading}>
            <h3 id="parcel-gn-title" className={styles.sectionTitle}>GN division mapping</h3>
            <span className={gnNeedsReview ? styles.reviewBadge : styles.matchedBadge}>
              {gnMapping.reviewStatusDisplay}
            </span>
          </div>
          <div className={styles.grid}>
            {gnNeedsReview ? (
              <>
                <DetailRow label="From source administrative code" value={formatGnDivisionAssignment(gnMapping.sourceFromAdministrativeCode)} />
                <DetailRow label="At parcel map marker" value={formatGnDivisionAssignment(gnMapping.gnAtMapMarker)} />
              </>
            ) : (
              <>
                <DetailRow label="GN division name" value={gnMapping.primaryAssignment?.gnName ?? "Unavailable"} />
                <DetailRow label="DS division" value={displayOrUnavailable(gnMapping.primaryAssignment?.divisionalSecretariat)} />
              </>
            )}
          </div>
          <p className={styles.inlineNote}>{gnMapping.provenanceLabel}</p>
        </section>
      ) : null}

      <section className={styles.card} aria-labelledby="parcel-evidence-title">
        <h3 id="parcel-evidence-title" className={styles.sectionTitle}>Recorded parcel evidence</h3>
        <div className={styles.grid}>
          <DetailRow label="Environmental restrictions recorded" value={String(parcel.environmentalRestrictionCount)} />
          <DetailRow label="Infrastructure features recorded" value={String(parcel.infrastructureFeatureCount)} />
        </div>
       
      </section>

      <details className={styles.technicalDetails}>
        <summary>Source data and technical identifiers</summary>
        <div className={styles.grid}>
          <DetailRow label="Centroid" value={`${parcel.spatial.centroidLatitude}, ${parcel.spatial.centroidLongitude}`} />
          <DetailRow label="Coordinate system" value={displayOrUnavailable(parcel.spatial.coordinateSystem)} />
          {parcel.cadastralSource ? (
            <>
              <DetailRow label="Source parcel ID" value={displayOrUnavailable(parcel.cadastralSource.sourceFeatureId)} />
              <DetailRow label="Source land-use code" value={displayOrUnavailable(parcel.cadastralSource.sourceReportedLandUseCode)} />
              <DetailRow
                label="Original source land-use text"
                value={displayOrUnavailable(
                  sourceReportedLandUseEnglishDisplay(parcel.cadastralSource),
                )}
              />
              <DetailRow label="Source parcel status code" value={displayOrUnavailable(parcel.cadastralSource.sourceParcelStatusCode)} />
              <DetailRow label="Research record ID" value={displayOrUnavailable(parcel.cadastralSource.researchRecordId ?? parcel.availability?.researchRecordId)} />
              <DetailRow label="Cadastral source GN index" value={displayOrUnavailable(parcel.cadastralSource.gnIndex)} />
            </>
          ) : null}
          {gnMapping ? (
            <>
              <DetailRow label="Source GN index (DCS code)" value={displayOrUnavailable(gnMapping.sourceGnIndex)} />
              {gnMapping.codeDistrict ? <DetailRow label="Code district" value={gnMapping.codeDistrict} /> : null}
            </>
          ) : null}
        </div>
      </details>

      <section className={styles.card} aria-labelledby="parcel-connections-title">
        <div className={styles.sectionHeading}>
          <div>
            <h3 id="parcel-connections-title" className={styles.sectionTitle}>Recorded connections</h3>
            <p className={styles.sectionSubtitle}>Relationships recorded for this parcel.</p>
          </div>
          <Link className={styles.explorerLink} href={`/land-intelligence/knowledge-graph?parcelId=${parcel.id}`}>
            Explore relationships
          </Link>
        </div>
        {relationshipsLoading ? <p className={styles.inlineNote}>Loading connections…</p> : null}
        {relationshipsError ? (
          <p className={styles.inlineNote} role="alert">Connections are unavailable right now: {relationshipsError}</p>
        ) : null}
        {!relationshipsLoading && !relationshipsError && relationships?.length === 0 ? (
          <p className={styles.inlineNote}>No connections are recorded for this parcel.</p>
        ) : null}
        {!relationshipsLoading && !relationshipsError && relationships && relationships.length > 0 ? (
          <ul className={styles.connectionList}>
            {relationships.map((item, index) => {
              const parcelIsSource = item.sourceNodeType === "LandParcel" && item.sourceNodeId === parcel.id;
              const relatedType = parcelIsSource ? item.targetNodeType : item.sourceNodeType;
              return (
                <li key={`${item.relationshipType}-${item.sourceNodeId}-${item.targetNodeId}-${index}`} className={styles.connectionCard}>
                  <span className={styles.connectionType}>{readableType(item.relationshipType)}</span>
                  <strong>{parcelConnectionDescription(item, parcel)}</strong>
                  <span className={styles.connectionKind}>{readableType(relatedType)}</span>
                </li>
              );
            })}
          </ul>
        ) : null}
      </section>
    </div>
  );
}


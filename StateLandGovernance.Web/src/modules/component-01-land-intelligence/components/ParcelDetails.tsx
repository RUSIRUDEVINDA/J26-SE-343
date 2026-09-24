import type {
  LandParcelResponse,
  LandRelationshipResponse,
  SpatialConstraintResponse,
} from "../types/landIntelligence";
import {
  landCategoryTypeLabels,
  landUseTypeLabels,
  spatialConstraintTypeLabels,
  restrictionSeverityLabels,
} from "../utils/enumMappings";
import { displayOrUnavailable, displayOrUnknown, formatArea } from "../utils/formatters";
import styles from "./ParcelDetails.module.css";
import listStyles from "./Lists.module.css";

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <span className={styles.value}>{value}</span>
    </div>
  );
}

export function ParcelDetails({
  parcel,
  constraints,
  relationships,
  relationshipsError,
  relationshipsLoading,
}: {
  parcel: LandParcelResponse;
  constraints: SpatialConstraintResponse[];
  relationships?: LandRelationshipResponse[] | null;
  relationshipsError?: string | null;
  relationshipsLoading?: boolean;
}) {
  return (
    <div>
      <div className={styles.card}>
        <div className={styles.grid}>
          <DetailRow
            label="Cadastral number"
            value={displayOrUnknown(parcel.identifier.cadastralNumber)}
          />
          <DetailRow label="Parcel UUID" value={parcel.id} />
          <DetailRow
            label="Survey plan reference"
            value={displayOrUnavailable(parcel.identifier.surveyPlanReference)}
          />
          <DetailRow
            label="Category"
            value={
              landCategoryTypeLabels[parcel.category.type] ??
              displayOrUnavailable(parcel.category.description)
            }
          />
          <DetailRow
            label="Current use"
            value={
              parcel.currentUse
                ? landUseTypeLabels[parcel.currentUse.type] ??
                  displayOrUnavailable(parcel.currentUse.description)
                : "Unavailable"
            }
          />
          <DetailRow
            label="Area"
            value={formatArea(parcel.area.value, parcel.area.unit)}
          />
          <DetailRow
            label="Province"
            value={displayOrUnavailable(parcel.location.province)}
          />
          <DetailRow
            label="District"
            value={displayOrUnavailable(parcel.location.district)}
          />
          <DetailRow
            label="Divisional secretariat"
            value={displayOrUnavailable(parcel.location.divisionalSecretariat)}
          />
          <DetailRow
            label="Grama Niladhari division"
            value={displayOrUnavailable(
              parcel.location.gramaNiladhariDivision,
            )}
          />
          <DetailRow
            label="Soil type"
            value={displayOrUnavailable(parcel.characteristics?.soilType)}
          />
          <DetailRow
            label="Terrain"
            value={displayOrUnavailable(
              parcel.characteristics?.terrainDescription,
            )}
          />
          <DetailRow
            label="Elevation (m)"
            value={
              parcel.characteristics?.elevationMeters == null
                ? "Unavailable"
                : String(parcel.characteristics.elevationMeters)
            }
          />
          <DetailRow
            label="Centroid"
            value={`${parcel.spatial.centroidLatitude}, ${parcel.spatial.centroidLongitude}`}
          />
          <DetailRow
            label="Coordinate system"
            value={displayOrUnavailable(parcel.spatial.coordinateSystem)}
          />
          <DetailRow
            label="Spatial constraints (count)"
            value={String(parcel.spatialConstraintCount)}
          />
          <DetailRow
            label="Environmental restrictions (count)"
            value={String(parcel.environmentalRestrictionCount)}
          />
          <DetailRow
            label="Infrastructure features (count)"
            value={String(parcel.infrastructureFeatureCount)}
          />
          <DetailRow
            label="Regulatory references (count)"
            value={String(parcel.regulatoryReferenceCount)}
          />
        </div>
      </div>

      <h3 className={styles.sectionTitle}>Spatial constraints</h3>
      {constraints.length === 0 ? (
        <div className={styles.note} role="note">
          <p className={listStyles.empty}>No recorded constraints.</p>
          <p className={styles.noteText}>
            This does not mean the parcel is safe, unrestricted, or free of
            constraints — only that none were returned by the API for this
            record.
          </p>
        </div>
      ) : (
        <ul className={listStyles.list}>
          {constraints.map((constraint) => (
            <li key={constraint.id} className={listStyles.item}>
              <p className={listStyles.meta}>
                <strong>
                  {spatialConstraintTypeLabels[constraint.type] ?? "Unknown"}
                </strong>{" "}
                · {restrictionSeverityLabels[constraint.severity] ?? "Unknown"}
              </p>
              <p>{displayOrUnavailable(constraint.description)}</p>
            </li>
          ))}
        </ul>
      )}

      <h3 className={styles.sectionTitle}>Knowledge graph relationships</h3>
      <p className={styles.noteText}>
        Loaded independently from Neo4j. A graph failure does not block parcel
        facts above. Neo4j is not required for search or recommendations.
      </p>
      {relationshipsLoading ? (
        <p className={listStyles.empty}>Loading relationships…</p>
      ) : null}
      {relationshipsError ? (
        <p className={styles.noteText} role="alert">
          Relationships unavailable: {relationshipsError}
        </p>
      ) : null}
      {!relationshipsLoading &&
      !relationshipsError &&
      relationships &&
      relationships.length === 0 ? (
        <p className={listStyles.empty}>No relationships returned.</p>
      ) : null}
      {relationships && relationships.length > 0 ? (
        <ul className={listStyles.list}>
          {relationships.map((item, index) => (
            <li
              key={`${item.relationshipType}-${item.sourceNodeId}-${index}`}
              className={listStyles.item}
            >
              <p className={listStyles.meta}>
                <strong>{item.relationshipType}</strong>
              </p>
              <p>
                {item.sourceNodeType} ({item.sourceNodeId}) →{" "}
                {item.targetNodeType} ({item.targetNodeId})
              </p>
              {item.description ? (
                <p className={listStyles.meta}>
                  {displayOrUnavailable(item.description)}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

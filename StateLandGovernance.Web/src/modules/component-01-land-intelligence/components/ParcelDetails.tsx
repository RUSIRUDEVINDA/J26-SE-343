import type {
  LandParcelResponse,
  SpatialConstraintResponse,
} from "../types/landIntelligence";
import {
  landCategoryTypeLabels,
  landUseTypeLabels,
  spatialConstraintTypeLabels,
  restrictionSeverityLabels,
} from "../utils/enumMappings";
import { displayOrUnknown, formatArea } from "../utils/formatters";
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
}: {
  parcel: LandParcelResponse;
  constraints: SpatialConstraintResponse[];
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
            label="Category"
            value={
              landCategoryTypeLabels[parcel.category.type] ??
              displayOrUnknown(parcel.category.description)
            }
          />
          <DetailRow
            label="Current use"
            value={
              parcel.currentUse
                ? landUseTypeLabels[parcel.currentUse.type] ??
                  displayOrUnknown(parcel.currentUse.description)
                : "Unknown"
            }
          />
          <DetailRow
            label="Area"
            value={formatArea(parcel.area.value, parcel.area.unit)}
          />
          <DetailRow
            label="Province"
            value={displayOrUnknown(parcel.location.province)}
          />
          <DetailRow
            label="District"
            value={displayOrUnknown(parcel.location.district)}
          />
          <DetailRow
            label="Divisional secretariat"
            value={displayOrUnknown(parcel.location.divisionalSecretariat)}
          />
          <DetailRow
            label="Soil type"
            value={displayOrUnknown(parcel.characteristics?.soilType)}
          />
          <DetailRow
            label="Centroid"
            value={`${parcel.spatial.centroidLatitude}, ${parcel.spatial.centroidLongitude}`}
          />
          <DetailRow
            label="Spatial constraints (count)"
            value={String(parcel.spatialConstraintCount)}
          />
          <DetailRow
            label="Environmental restrictions (count)"
            value={String(parcel.environmentalRestrictionCount)}
          />
        </div>
      </div>

      <h3 className={styles.sectionTitle}>Spatial constraints</h3>
      {constraints.length === 0 ? (
        <p className={listStyles.empty}>No spatial constraints returned.</p>
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
              <p>{displayOrUnknown(constraint.description)}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

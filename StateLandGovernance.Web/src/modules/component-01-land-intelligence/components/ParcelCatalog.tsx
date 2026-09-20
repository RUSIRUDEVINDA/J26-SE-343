import type { LandSearchResultResponse } from "../types/landIntelligence";
import {
  landCategoryTypeLabels,
  landUseTypeLabels,
} from "../utils/enumMappings";
import { formatArea } from "../utils/formatters";
import styles from "./ParcelCatalog.module.css";

export function ParcelCatalog({
  parcels,
  totalCount,
  selectedId,
  onSelect,
}: {
  parcels: LandSearchResultResponse[];
  totalCount: number;
  selectedId?: string | null;
  onSelect: (id: string) => void;
}) {
  return (
    <section className={styles.wrap} aria-label="Available land parcels">
      <div className={styles.header}>
        <h2 className={styles.title}>Available land parcels</h2>
        <p className={styles.count}>
          Showing {parcels.length} of {totalCount} indexed parcel
          {totalCount === 1 ? "" : "s"}
        </p>
      </div>
      <div className={styles.tableScroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Cadastral no.</th>
              <th scope="col">District</th>
              <th scope="col">Category</th>
              <th scope="col">Use</th>
              <th scope="col">Area</th>
              <th scope="col">Centroid</th>
              <th scope="col">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {parcels.map((parcel) => {
              const selected = selectedId === parcel.id;
              return (
                <tr
                  key={parcel.id}
                  className={selected ? styles.rowSelected : undefined}
                >
                  <td>
                    <button
                      type="button"
                      className={styles.rowButton}
                      onClick={() => onSelect(parcel.id)}
                    >
                      {parcel.cadastralNumber}
                    </button>
                  </td>
                  <td>{parcel.district}</td>
                  <td>
                    {landCategoryTypeLabels[parcel.categoryType] ?? "Unknown"}
                  </td>
                  <td>
                    {parcel.currentUseType
                      ? landUseTypeLabels[parcel.currentUseType] ?? "Unknown"
                      : "—"}
                  </td>
                  <td>{formatArea(parcel.areaValue, parcel.areaUnit)}</td>
                  <td className={styles.mono}>
                    {parcel.centroidLatitude.toFixed(5)},{" "}
                    {parcel.centroidLongitude.toFixed(5)}
                  </td>
                  <td className={styles.actionsCell}>
                    <button
                      type="button"
                      className={styles.viewLink}
                      onClick={() => onSelect(parcel.id)}
                    >
                      View details
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

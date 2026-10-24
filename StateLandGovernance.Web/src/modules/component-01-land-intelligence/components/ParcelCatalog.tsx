import type { LandSearchResultResponse } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { sourceReportedLandUseDisplay } from "../utils/sourceReportedCadastre";
import { displayOrUnavailable, formatArea } from "../utils/formatters";
import styles from "./ParcelCatalog.module.css";

export const PARCEL_CATALOG_PAGE_SIZES = [20, 40, 60] as const;

export type ParcelCatalogPageSize = (typeof PARCEL_CATALOG_PAGE_SIZES)[number];

export function ParcelCatalog({
  parcels,
  totalCount,
  selectedId,
  onSelect,
  currentPage = 1,
  pageSize = 20,
  onPageChange,
  onPageSizeChange,
}: {
  parcels: LandSearchResultResponse[];
  totalCount: number;
  selectedId?: string | null;
  onSelect: (id: string) => void;
  currentPage?: number;
  pageSize?: number;
  onPageChange?: (page: number) => void;
  onPageSizeChange?: (pageSize: ParcelCatalogPageSize) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const activePage = Math.min(Math.max(1, currentPage), totalPages);
  const rangeStart = totalCount === 0 ? 0 : (activePage - 1) * pageSize + 1;
  const rangeEnd = Math.min(activePage * pageSize, totalCount);
  const showPager = Boolean(onPageChange);
  const pageSizeControl = showPager ? (
    <div className={styles.pageSizeControl}>
      <label htmlFor="parcel-catalog-page-size">Rows per page</label>
      <select
        id="parcel-catalog-page-size"
        className={styles.pageSizeSelect}
        value={pageSize}
        disabled={!onPageSizeChange}
        onChange={(e) =>
          onPageSizeChange?.(
            Number(e.target.value) as ParcelCatalogPageSize,
          )
        }
      >
        {PARCEL_CATALOG_PAGE_SIZES.map((size) => (
          <option key={size} value={size}>
            {size}
          </option>
        ))}
      </select>
    </div>
  ) : null;

  const rangeSummary =
    totalCount === 0
      ? "No indexed parcels"
      : `Showing ${rangeStart}–${rangeEnd} of ${totalCount} indexed parcel${totalCount === 1 ? "" : "s"}`;

  return (
    <section className={styles.wrap} aria-label="Available land parcels">
      <div className={styles.header}>
        <h2 className={styles.title}>Indexed land parcels</h2>
        {pageSizeControl}
      </div>
      <div className={styles.tableScroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Cadastral number</th>
              <th scope="col">District</th>
              <th scope="col">Source-reported land use</th>
              <th scope="col">GN division index</th>
              <th scope="col">Area</th>
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
                    {parcel.cadastralSource
                      ? sourceReportedLandUseDisplay(parcel.cadastralSource) ||
                        "Not recorded"
                      : parcel.currentUseType
                        ? (landUseTypeLabels[parcel.currentUseType] ??
                          "Not recorded")
                        : "Not recorded"}
                  </td>
                  <td>
                    {displayOrUnavailable(parcel.cadastralSource?.gnIndex)}
                  </td>
                  <td>{formatArea(parcel.areaValue, parcel.areaUnit)}</td>
                  <td className={styles.actionsCell}>
                    <button
                      type="button"
                      className={styles.viewLink}
                      onClick={() => onSelect(parcel.id)}
                    >
                      View details
                    </button>
                    <a
                      className={styles.viewLink}
                      href={`/land-intelligence/knowledge-graph?parcelId=${parcel.id}`}
                    >
                      Relationships
                    </a>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className={styles.pagination}>
        <p className={styles.count} role="status">
          {rangeSummary}
        </p>
        {showPager ? (
          <div className={styles.pageNav}>
            <button
              type="button"
              className={styles.pageButton}
              disabled={activePage <= 1}
              onClick={() => onPageChange?.(activePage - 1)}
              aria-label="Previous page"
            >
              Previous
            </button>
            <span className={styles.pageInfo}>
              Page {activePage} of {totalPages}
            </span>
            <button
              type="button"
              className={styles.pageButton}
              disabled={activePage >= totalPages}
              onClick={() => onPageChange?.(activePage + 1)}
              aria-label="Next page"
            >
              Next
            </button>
          </div>
        ) : null}
      </div>
    </section>
  );
}

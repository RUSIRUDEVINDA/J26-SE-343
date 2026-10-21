import type {
  LandSearchResultResponse,
  ResearchSupplementParcelResponse,
} from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { sourceReportedLandUseDisplay } from "../utils/sourceReportedCadastre";
import { formatArea } from "../utils/formatters";
import styles from "./ParcelCatalog.module.css";

export const PARCEL_CATALOG_PAGE_SIZES = [20, 40, 60] as const;

export type ParcelCatalogPageSize = (typeof PARCEL_CATALOG_PAGE_SIZES)[number];

export function ParcelCatalog({
  parcels,
  totalCount,
  selectedId,
  onSelect,
  supplementByParcelId,
  currentPage = 1,
  pageSize = 20,
  onPageChange,
  onPageSizeChange,
}: {
  parcels: LandSearchResultResponse[];
  totalCount: number;
  selectedId?: string | null;
  onSelect: (id: string) => void;
  supplementByParcelId?: Map<string, ResearchSupplementParcelResponse>;
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

  return (
    <section className={styles.wrap} aria-label="Available land parcels">
      <div className={styles.header}>
        <h2 className={styles.title}>Indexed land parcels</h2>
        <p className={styles.count}>
          {totalCount === 0
            ? "No indexed parcels"
            : `Showing ${rangeStart}–${rangeEnd} of ${totalCount} indexed parcel${totalCount === 1 ? "" : "s"}`}
        </p>
      </div>
      <div className={styles.tableScroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Cadastral number</th>
              <th scope="col">District</th>
              <th scope="col">Source-reported land use</th>
              <th scope="col">Project GIS classification</th>
              <th scope="col">Area</th>
              <th scope="col">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {parcels.map((parcel) => {
              const selected = selectedId === parcel.id;
              const supplementRow = supplementByParcelId?.get(parcel.id);
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
                    {supplementRow ? (
                      <span title={supplementRow.classificationBasis}>
                        {supplementRow.supplementGroup}
                        <small style={{ display: "block" }}>
                          {supplementRow.classificationBasis ===
                          "GIS-derived LUPPD coverage (at least 80%)"
                            ? `Project GIS · ${supplementRow.gisOverlapPercent.toFixed(1)}% mapped coverage`
                            : "Project GIS · original source label"}
                        </small>
                      </span>
                    ) : (
                      "Not recorded"
                    )}
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
      {showPager ? (
        <div className={styles.pagination}>
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
        </div>
      ) : null}
    </section>
  );
}

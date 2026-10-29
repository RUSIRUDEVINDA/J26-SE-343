import type { ApprovedPreliminaryParcelSummaryResponse } from "../types/landIntelligence";
import { formatApplicantDateTime } from "../utils/applicantValuationDisplay";
import { displayOrUnavailable, formatArea } from "../utils/formatters";
import styles from "./ParcelCatalog.module.css";

export const APPROVED_PARCELS_PAGE_SIZES = [20, 40, 60] as const;

export type ApprovedParcelsPageSize =
  (typeof APPROVED_PARCELS_PAGE_SIZES)[number];

export function ApprovedParcelsCatalog({
  parcels,
  totalCount,
  currentPage = 1,
  pageSize = 20,
  onPageChange,
  onPageSizeChange,
}: {
  parcels: ApprovedPreliminaryParcelSummaryResponse[];
  totalCount: number;
  currentPage?: number;
  pageSize?: number;
  onPageChange?: (page: number) => void;
  onPageSizeChange?: (pageSize: ApprovedParcelsPageSize) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const activePage = Math.min(Math.max(1, currentPage), totalPages);
  const rangeStart = totalCount === 0 ? 0 : (activePage - 1) * pageSize + 1;
  const rangeEnd = Math.min(activePage * pageSize, totalCount);
  const showPager = Boolean(onPageChange);

  const rangeSummary =
    totalCount === 0
      ? "No approved preliminary requests"
      : `Showing ${rangeStart}–${rangeEnd} of ${totalCount} approved parcel${totalCount === 1 ? "" : "s"}`;

  return (
    <section
      className={styles.wrap}
      aria-label="Parcels with approved preliminary lease interest"
    >
      <div className={styles.header}>
        <h2 className={styles.title}>Approved preliminary requests</h2>
        {showPager ? (
          <div className={styles.pageSizeControl}>
            <label htmlFor="approved-parcels-page-size">Rows per page</label>
            <select
              id="approved-parcels-page-size"
              className={styles.pageSizeSelect}
              value={pageSize}
              disabled={!onPageSizeChange}
              onChange={(e) =>
                onPageSizeChange?.(
                  Number(e.target.value) as ApprovedParcelsPageSize,
                )
              }
            >
              {APPROVED_PARCELS_PAGE_SIZES.map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
          </div>
        ) : null}
      </div>
      <div className={styles.tableScroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Cadastral number</th>
              <th scope="col">Location</th>
              <th scope="col">Area</th>
              <th scope="col">Source-reported land use</th>
              <th scope="col">Approval date</th>
              <th scope="col">Request reference</th>
              <th scope="col">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {parcels.map((parcel) => (
              <tr key={parcel.landParcelId}>
                <td>{parcel.cadastralNumber}</td>
                <td>
                  {[parcel.district, parcel.province]
                    .filter(Boolean)
                    .join(", ")}
                </td>
                <td>{formatArea(parcel.areaValue, parcel.areaUnit)}</td>
                <td>
                  {displayOrUnavailable(parcel.sourceReportedLandUse)}
                </td>
                <td>{formatApplicantDateTime(parcel.approvedAtUtc)}</td>
                <td>{displayOrUnavailable(parcel.requestReference)}</td>
                <td className={styles.actionsCell}>
                  <a
                    className={styles.viewLink}
                    href={`/land-intelligence/parcels/${parcel.landParcelId}`}
                  >
                    View parcel
                  </a>
                </td>
              </tr>
            ))}
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

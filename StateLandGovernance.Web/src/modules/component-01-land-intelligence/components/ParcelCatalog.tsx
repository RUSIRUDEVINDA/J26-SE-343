import type { LandSearchResultResponse, ResearchSupplementParcelResponse } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { sourceReportedLandUseDisplay } from "../utils/sourceReportedCadastre";
import { formatArea } from "../utils/formatters";
import styles from "./ParcelCatalog.module.css";

export function ParcelCatalog({
  parcels,
  totalCount,
  selectedId,
  onSelect,
  supplementByParcelId,
  currentPage,
  pageSize,
  onPageChange,
  onCadastralSearch,
}: {
  parcels: LandSearchResultResponse[];
  totalCount: number;
  selectedId?: string | null;
  onSelect: (id: string) => void;
  supplementByParcelId?: Map<string, ResearchSupplementParcelResponse>;
  pageSize?: number;
  onCadastralSearch?: (query: string) => void;
} & (
  | { currentPage: number; onPageChange: (page: number) => void }
  | { currentPage?: number; onPageChange?: undefined }
)) {
  const totalPages = pageSize ? Math.ceil(totalCount / pageSize) : 1;

  return (
    <section className={styles.wrap} aria-label="Available land parcels">
      <div className={styles.header}>
        <h2 className={styles.title}>Indexed land parcels</h2>
        <p className={styles.count}>
          Showing {parcels.length} of {totalCount} indexed parcel
          {totalCount === 1 ? "" : "s"}
        </p>
      </div>
      {onCadastralSearch && (
        <div className={styles.searchRow}>
          <input
            type="text"
            className={styles.searchInput}
            placeholder="Search by cadastral number..."
            onChange={(e) => onCadastralSearch(e.target.value)}
            aria-label="Search parcels by cadastral number"
          />
        </div>
      )}
      <div className={styles.tableScroll}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Cadastral no.</th>
              <th scope="col">District</th>
              <th scope="col">Source-reported land use</th>
              <th scope="col">Project research group</th>
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
                        "—"
                      : parcel.currentUseType
                        ? landUseTypeLabels[parcel.currentUseType] ?? "Unknown"
                        : "—"}
                  </td>
                  <td>
                    {supplementByParcelId?.get(parcel.id) ? (
                      <span title={supplementByParcelId.get(parcel.id)?.classificationBasis}>
                        {supplementByParcelId.get(parcel.id)?.supplementGroup}
                        <small style={{ display: "block" }}>
                          {supplementByParcelId.get(parcel.id)?.classificationBasis ===
                          "GIS-derived LUPPD coverage (at least 80%)"
                            ? `${supplementByParcelId.get(parcel.id)?.gisOverlapPercent.toFixed(1)}% mapped coverage`
                            : "Original source label"}
                        </small>
                      </span>
                    ) : "—"}
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
      {onPageChange && totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            type="button"
            className={styles.pageButton}
            disabled={currentPage === 1}
            onClick={() => onPageChange(currentPage - 1)}
            aria-label="Previous page"
          >
            Previous
          </button>
          <span className={styles.pageInfo}>
            Page {currentPage} of {totalPages}
          </span>
          <button
            type="button"
            className={styles.pageButton}
            disabled={currentPage === totalPages}
            onClick={() => onPageChange(currentPage + 1)}
            aria-label="Next page"
          >
            Next
          </button>
        </div>
      )}
    </section>
  );
}

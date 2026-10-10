import React from "react";
import { formatGovernanceTime, conflictTone } from "../adapters/presentation";
import { Search } from "lucide-react";
import type { GovernanceCaseAssessment, GovernanceFilter } from "../types/governance";
import { StatusBadge } from "@/shared/components/StatusBadge";
import styles from "./AssessmentTable.module.css";

interface Props {
  cases: GovernanceCaseAssessment[];
  selectedCaseId: string | null;
  onSelectCase: (caseItem: GovernanceCaseAssessment) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  activeFilter: GovernanceFilter;
  onFilterChange: (filter: GovernanceFilter) => void;
}

export function AssessmentTable({
  cases,
  selectedCaseId,
  onSelectCase,
  searchQuery,
  onSearchChange,
  activeFilter,
  onFilterChange,
}: Props) {
  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <div className={styles.searchWrapper}>
          <Search className={styles.searchIcon} size={16} aria-hidden="true" />
          <input
            type="search"
            className={styles.searchInput}
            placeholder="Search by Case ID or Parcel..."
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            aria-label="Search cases by Case ID or Parcel Reference"
          />
        </div>

        <div className={styles.filterPills} role="group" aria-label="Filter case assessments by governance status">
          <button
            type="button"
            className={`${styles.pill} ${activeFilter === "ALL" ? styles.pillActive : ""}`}
            aria-pressed={activeFilter === "ALL"}
            onClick={() => onFilterChange("ALL")}
          >
            All Cases
          </button>
          <button
            type="button"
            className={`${styles.pill} ${activeFilter === "REVIEW_REQUIRED" ? styles.pillActive : ""}`}
            aria-pressed={activeFilter === "REVIEW_REQUIRED"}
            onClick={() => onFilterChange("REVIEW_REQUIRED")}
          >
            Review Required
          </button>
          <button
            type="button"
            className={`${styles.pill} ${activeFilter === "CLEAR" ? styles.pillActive : ""}`}
            aria-pressed={activeFilter === "CLEAR"}
            onClick={() => onFilterChange("CLEAR")}
          >
            Clear / Compliant
          </button>
          <button
            type="button"
            className={`${styles.pill} ${activeFilter === "INSUFFICIENT" ? styles.pillActive : ""}`}
            aria-pressed={activeFilter === "INSUFFICIENT"}
            onClick={() => onFilterChange("INSUFFICIENT")}
          >
            Pending Records
          </button>
        </div>
      </div>

      <div className={styles.tableResponsive}>
        <table className={styles.table} aria-label="Governance Case Assessments Table">
          <thead>
            <tr>
              <th scope="col">Case / Parcel</th>
              <th scope="col">Applicant</th>
              <th scope="col">Governance Status</th>
              <th scope="col">Compliance</th>
              <th scope="col">Conflict Check</th>
              <th scope="col">Early Screening</th>
              <th scope="col">Evaluated</th>
              <th scope="col">Action</th>
            </tr>
          </thead>
          <tbody>
            {cases.length === 0 ? (
              <tr>
                <td colSpan={8} className={styles.emptyResults}>
                  No governance assessments found matching your filter criteria.
                </td>
              </tr>
            ) : (
              cases.map((item) => {
                const isSelected = item.assessmentId === selectedCaseId;
                return (
                  <tr
                    key={item.assessmentId}
                    className={`${styles.row} ${isSelected ? styles.rowSelected : ""}`}
                    onClick={() => onSelectCase(item)}
                    data-selected={isSelected}
                  >
                    <td>
                      <span className={styles.caseId}>{item.caseId}</span>
                      <span className={styles.parcelRef}>{item.parcelReference}</span>
                    </td>
                    <td>
                      <p className={styles.applicantText}>{item.applicantReference}</p>
                    </td>
                    <td>
                      <StatusBadge
                        tone={
                          item.overallStatus === "Clear"
                            ? "success"
                            : item.overallStatus === "ReviewRequired"
                            ? "danger"
                            : "neutral"
                        }
                      >
                        {item.overallStatus === "ReviewRequired"
                          ? "Review Required"
                          : item.overallStatus === "Clear"
                          ? "Clear"
                          : "Pending Info"}
                      </StatusBadge>
                    </td>
                    <td>
                      <StatusBadge
                        tone={
                          item.complianceStatus === "Compliant"
                            ? "success"
                            : item.complianceStatus === "NonCompliant"
                            ? "danger"
                            : item.complianceStatus === "Conditional"
                            ? "warning"
                            : "neutral"
                        }
                      >
                        {item.complianceStatus}
                      </StatusBadge>
                    </td>
                    <td>
                      <StatusBadge tone={conflictTone(item.conflictStatus)}>
                        {item.conflictStatus === "ConflictDetected" ? "Conflict Detected" : item.conflictStatus}
                      </StatusBadge>
                    </td>
                    <td>
                      <StatusBadge
                        tone={
                          item.earlyScreeningStatus === "Clear"
                            ? "success"
                            : item.earlyScreeningStatus === "ReviewRequired"
                            ? "danger"
                            : "neutral"
                        }
                      >
                        {item.earlyScreeningStatus}
                      </StatusBadge>
                    </td>
                    <td>
                      <span className={styles.timeText}>{formatGovernanceTime(item.evaluatedAt)}</span>
                    </td>
                    <td>
                      <button
                        type="button"
                        className={styles.selectButton}
                        onClick={(e) => {
                          e.stopPropagation();
                          onSelectCase(item);
                        }}
                      >
                        {isSelected ? "Selected" : "Review"}
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

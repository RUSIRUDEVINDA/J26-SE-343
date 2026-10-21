"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { Button } from "@/shared/components/Button";
import { LoadingState } from "@/shared/components/LoadingState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { EmptyState } from "@/shared/components/EmptyState";
import {
  workflowGovernanceNavItems,
  WORKFLOW_GOVERNANCE_BASE,
} from "@/shared/constants/navigation";
import { listCases } from "@/modules/workflow-governance/services/workflowGovernanceApi";
import type { LeaseCaseSummary } from "@/modules/workflow-governance/types/caseManagement";
import styles from "./CasesPage.module.css";

const PAGE_SIZE = 15;

export default function CasesQueuePage() {
  const [cases, setCases] = useState<LeaseCaseSummary[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [page, setPage] = useState<number>(0);
  const [myCasesOnly, setMyCasesOnly] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchCases = async (targetPage: number, mineOnly: boolean) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await listCases(targetPage * PAGE_SIZE, PAGE_SIZE, mineOnly, "officer");
      setCases(response.items || []);
      setTotalCount(response.totalCount || 0);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to load lease cases.";
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchCases(page, myCasesOnly);
  }, [page, myCasesOnly]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <PageLayout
      navigationItems={workflowGovernanceNavItems}
      navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
      navigationLabel="Workflow Governance navigation"
    >
      <p><Link href="/workflow-governance/assignments">Intake assignment queue (coordinator access required)</Link></p>
      <PageHeading
        title="Lease Case Governance Queue"
        subtitle="Manage persisted state land lease proposals, verify extracted document facts, and publish immutable verification snapshots."
        action={
          <Link href="/workflow-governance/applicant/intake">
            <Button variant="primary">New Case Intake</Button>
          </Link>
        }
      />

      {/* KPI Cards */}
      <div className="cardGrid" style={{ marginBottom: "1.5rem" }}>
        <div className="statCard">
          <p className="statLabel">Total Cases</p>
          <p className="statValue">{totalCount}</p>
          <p className="statHint">Persisted in governance repository</p>
        </div>
        <div className="statCard">
          <p className="statLabel">Snapshot Published</p>
          <p className="statValue">
            {cases.filter((c) => c.currentVerifiedFactSnapshotId).length}
          </p>
          <p className="statHint">Ready for institutional review</p>
        </div>
        <div className="statCard">
          <p className="statLabel">Screening Gate</p>
          <p className="statValue" style={{ color: "#b45309" }}>Pending</p>
          <p className="statHint">Regulatory / Conflict / Early Screening</p>
        </div>
        <div className="statCard">
          <p className="statLabel">Role & Scope</p>
          <p className="statValue" style={{ fontSize: "1.25rem" }}>Officer</p>
          <p className="statHint">Your active case assignments</p>
        </div>
      </div>

      {/* Filter and Control Bar */}
      <div className={styles.filterBar}>
        <div className={styles.filterOptions}>
          <label className={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={myCasesOnly}
              onChange={(e) => {
                setMyCasesOnly(e.target.checked);
                setPage(0);
              }}
            />
            <span>Filter to my registered cases</span>
          </label>
        </div>
        <div className={styles.paginationControls}>
          <span className={styles.pageIndicator}>
            Page {page + 1} of {totalPages} ({totalCount} cases)
          </span>
          <Button
            variant="secondary"
            disabled={page === 0 || isLoading}
            onClick={() => setPage((p) => Math.max(0, p - 1))}
          >
            Previous
          </Button>
          <Button
            variant="secondary"
            disabled={page >= totalPages - 1 || isLoading}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </Button>
        </div>
      </div>

      {/* Main Content Area */}
      {isLoading ? (
        <LoadingState message="Loading lease cases from governance repository..." />
      ) : error ? (
        <ErrorMessage
          title="Unable to Retrieve Case Queue"
          message={error}
        />
      ) : cases.length === 0 ? (
        <EmptyState
          title="No Lease Cases Found"
          description={
            myCasesOnly
              ? "You have not registered any lease cases under your officer account yet."
              : "No lease cases have been registered in the governance system."
          }
          action={
            <Link href="/workflow-governance/applicant/intake">
              <Button variant="primary">Register Intake</Button>
            </Link>
          }
        />
      ) : (
        <div className={styles.tableContainer}>
          <table className={styles.caseTable}>
            <thead>
              <tr>
                <th>Reference</th>
                <th>Purpose & Extent</th>
                <th>Jurisdiction</th>
                <th>Snapshot</th>
                <th>Status</th>
                <th>Screening</th>
                <th>Submitted</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {cases.map((item) => {
                const extentDisplay = item.proposalIntake?.requestedExtentValue
                  ? `${item.proposalIntake.requestedExtentValue} ${item.proposalIntake.requestedExtentUnit || "Acres"}`
                  : "Not specified";

                const hasSnapshot = Boolean(item.currentVerifiedFactSnapshotId);

                return (
                  <tr key={item.id} className={styles.tableRow}>
                    <td className={styles.referenceCell}>
                      <span className={styles.refCode}>{item.applicationReference}</span>
                      <span className={styles.caseIdSub}>{item.id.slice(0, 8)}…</span>
                    </td>
                    <td>
                      <div className={styles.purposeText}>
                        {item.proposalIntake?.purpose || "Lease Proposal"}
                      </div>
                      <div className={styles.extentText}>{extentDisplay}</div>
                    </td>
                    <td>
                      <span className={styles.jurisdictionTag}>
                        {item.proposalIntake?.jurisdictionCode || "Universal"}
                      </span>
                    </td>
                    <td>
                      {hasSnapshot ? (
                        <StatusBadge tone="success">Snapshot Linked</StatusBadge>
                      ) : (
                        <StatusBadge tone="warning">Pending Verification</StatusBadge>
                      )}
                    </td>
                    <td>
                      <StatusBadge
                        tone={
                          item.status === "ApprovedWithConditions" || item.status === "HandedOff"
                            ? "success"
                            : item.status === "InWorkflow"
                            ? "primary"
                            : "neutral"
                        }
                      >
                        {item.status}
                      </StatusBadge>
                    </td>
                    <td>
                      <span className={styles.screeningPending}>
                        <span className={styles.pendingDot} /> Pending Phase 2
                      </span>
                    </td>
                    <td className={styles.dateCell}>
                      {new Date(item.createdAt).toLocaleDateString(undefined, {
                        year: "numeric",
                        month: "short",
                        day: "numeric",
                      })}
                    </td>
                    <td>
                      <Link href={`/workflow-governance/cases/${item.id}`}>
                        <Button variant="secondary">Review Workspace</Button>
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}

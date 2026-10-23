"use client";

import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { StatusBadge } from "@/shared/components/StatusBadge";
import {
  deleteLeaseInterestDraft,
  listMyLeaseInterestRequests,
} from "@/modules/component-01-land-intelligence/services/citizenWorkspaceApi";
import type { LeaseInterestRequestSummary } from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { leaseInterestPurposeDisplay } from "@/modules/component-01-land-intelligence/utils/leaseInterestPurposeDisplay";
import { useModalDialogA11y } from "@/modules/component-01-land-intelligence/hooks/useModalDialogA11y";
import {
  isLeaseInterestDraftStatus,
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "@/modules/component-01-land-intelligence/utils/leaseInterestStatus";
import styles from "./MyRequests.module.css";

const PAGE_SIZE = 10;
const MY_REQUESTS_PANEL_ID = "my-requests-panel";

type MyRequestsTab = "submitted" | "drafts";

const MY_REQUESTS_TABS: { value: MyRequestsTab; label: string }[] = [
  { value: "submitted", label: "Submitted" },
  { value: "drafts", label: "Drafts" },
];

function formatShortDate(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function sortSubmitted(
  rows: LeaseInterestRequestSummary[],
): LeaseInterestRequestSummary[] {
  return [...rows].sort((a, b) => {
    const ta = new Date(a.submittedAtUtc ?? a.updatedAtUtc).getTime();
    const tb = new Date(b.submittedAtUtc ?? b.updatedAtUtc).getTime();
    return tb - ta;
  });
}

function sortDrafts(
  rows: LeaseInterestRequestSummary[],
): LeaseInterestRequestSummary[] {
  return [...rows].sort((a, b) => {
    const ta = new Date(a.updatedAtUtc).getTime();
    const tb = new Date(b.updatedAtUtc).getTime();
    return tb - ta;
  });
}

function clampPage(page: number, itemCount: number): number {
  const totalPages = Math.max(1, Math.ceil(itemCount / PAGE_SIZE));
  return Math.min(Math.max(1, page), totalPages);
}

function paginate<T>(rows: T[], page: number): T[] {
  const start = (page - 1) * PAGE_SIZE;
  return rows.slice(start, start + PAGE_SIZE);
}

function RequestRow({
  item,
  onDeleted,
}: {
  item: LeaseInterestRequestSummary;
  onDeleted?: (requestId: string) => void;
}) {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const { dialogRef: confirmDialogRef } = useModalDialogA11y({
    open: confirmOpen,
    onClose: () => {
      if (!deleting) {
        setConfirmOpen(false);
      }
    },
    busy: deleting,
  });
  const isDraft = isLeaseInterestDraftStatus(item.status);
  const purpose = leaseInterestPurposeDisplay(
    item.requestedPurpose,
    item.status,
  );
  const reference =
    item.acknowledgementReference ??
    (isDraft ? `Draft · ${item.id.slice(0, 8)}` : item.id.slice(0, 8));
  const parcelLabel = item.cadastralNumber?.trim() || item.landParcelId.slice(0, 8);

  async function confirmDeleteDraft() {
    setDeleting(true);
    setDeleteError(null);
    try {
      await deleteLeaseInterestDraft(item.id);
      setConfirmOpen(false);
      onDeleted?.(item.id);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setDeleteError(err.detail || err.title);
      } else {
        setDeleteError("Unable to delete this draft.");
      }
    } finally {
      setDeleting(false);
    }
  }

  return (
    <li className={styles.row}>
      {confirmOpen ? (
        <div
          className={styles.modalOverlay}
          role="presentation"
          onClick={() => {
            if (!deleting) {
              setConfirmOpen(false);
            }
          }}
        >
          <div
            ref={confirmDialogRef}
            className={styles.modalDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby={`delete-draft-title-${item.id}`}
            aria-describedby={`delete-draft-body-${item.id}`}
            aria-busy={deleting}
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id={`delete-draft-title-${item.id}`} className={styles.modalTitle}>
              Delete this draft?
            </h2>
            <p id={`delete-draft-body-${item.id}`} className={styles.modalBody}>
              Parcel <strong className={styles.modalParcel}>{parcelLabel}</strong>{" "}
              ({reference}) will be removed from your drafts. Saved answers cannot be
              recovered.
            </p>
            <div className={styles.modalActions}>
              <Button
                type="button"
                variant="primary"
                className={styles.dangerFilledButton}
                disabled={deleting}
                onClick={() => void confirmDeleteDraft()}
              >
                {deleting ? "Deleting…" : "Delete draft"}
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={deleting}
                onClick={() => setConfirmOpen(false)}
              >
                Cancel
              </Button>
            </div>
          </div>
        </div>
      ) : null}
      <div className={styles.rowMain}>
        <div className={styles.rowPrimary}>
          <p className={styles.parcelId}>
            <span className={styles.srOnly}>Parcel </span>
            {parcelLabel}
          </p>
          <StatusBadge tone={leaseInterestStatusBadgeTone(item.status)}>
            {leaseInterestStatusLabel(item.status)}
          </StatusBadge>
        </div>
        <dl className={styles.rowMeta}>
          <div>
            <dt>Reference</dt>
            <dd className={styles.mono}>{reference}</dd>
          </div>
          {purpose ? (
            <div>
              <dt>Purpose</dt>
              <dd>{purpose}</dd>
            </div>
          ) : null}
          <div>
            <dt>{isDraft ? "Updated" : "Submitted"}</dt>
            <dd>
              {formatShortDate(
                isDraft ? item.updatedAtUtc : (item.submittedAtUtc ?? item.updatedAtUtc),
              )}
            </dd>
          </div>
        </dl>
        {deleteError ? (
          <p className={styles.rowDeleteError} role="alert">{deleteError}</p>
        ) : null}
      </div>
      <div className={styles.rowActions}>
        {isDraft ? (
          <>
            <Button
              href={`/land-intelligence/parcels/${item.landParcelId}/lease-interest`}
              variant="primary"
              className={styles.rowAction}
            >
              Continue draft
            </Button>
            <Button
              type="button"
              variant="ghost"
              className={styles.deleteAction}
              disabled={deleting || confirmOpen}
              onClick={() => setConfirmOpen(true)}
            >
              Delete draft
            </Button>
          </>
        ) : (
          <Button
            href={`/land-intelligence/my-requests/${item.id}`}
            variant="primary"
            className={styles.rowAction}
          >
            View status
          </Button>
        )}
      </div>
    </li>
  );
}

export default function MyRequestsPage() {
  const [items, setItems] = useState<LeaseInterestRequestSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<MyRequestsTab>("submitted");
  const [pageByTab, setPageByTab] = useState<Record<MyRequestsTab, number>>({
    submitted: 1,
    drafts: 1,
  });
  const tabRefs = useRef<Record<MyRequestsTab, HTMLButtonElement | null>>({
    submitted: null,
    drafts: null,
  });

  useEffect(() => {
    let cancelled = false;
    listMyLeaseInterestRequests()
      .then((rows) => {
        if (!cancelled) {
          setItems(rows);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          if (err instanceof ApiClientError) {
            setError(err.detail || err.title);
          } else {
            setError("Unable to load requests.");
          }
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const { drafts, submitted } = useMemo(() => {
    const draftRows: LeaseInterestRequestSummary[] = [];
    const submittedRows: LeaseInterestRequestSummary[] = [];
    for (const item of items) {
      if (isLeaseInterestDraftStatus(item.status)) {
        draftRows.push(item);
      } else {
        submittedRows.push(item);
      }
    }
    return {
      drafts: sortDrafts(draftRows),
      submitted: sortSubmitted(submittedRows),
    };
  }, [items]);

  const tabLists: Record<MyRequestsTab, LeaseInterestRequestSummary[]> = {
    submitted,
    drafts,
  };

  const activeList = tabLists[activeTab];
  const activePage = pageByTab[activeTab];
  const totalPages = Math.max(1, Math.ceil(activeList.length / PAGE_SIZE));
  const pageItems = paginate(activeList, activePage);

  function selectTab(next: MyRequestsTab) {
    if (next === activeTab) {
      return;
    }
    setActiveTab(next);
  }

  function onTabKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const index = MY_REQUESTS_TABS.findIndex(
      (option) => option.value === activeTab,
    );
    let nextIndex = index;
    if (event.key === "ArrowRight") {
      nextIndex = (index + 1) % MY_REQUESTS_TABS.length;
    } else if (event.key === "ArrowLeft") {
      nextIndex =
        (index - 1 + MY_REQUESTS_TABS.length) % MY_REQUESTS_TABS.length;
    } else if (event.key === "Home") {
      nextIndex = 0;
    } else if (event.key === "End") {
      nextIndex = MY_REQUESTS_TABS.length - 1;
    } else {
      return;
    }
    event.preventDefault();
    const next = MY_REQUESTS_TABS[nextIndex].value;
    selectTab(next);
    tabRefs.current[next]?.focus();
  }

  function setActiveTabPage(nextPage: number) {
    setPageByTab((prev) => ({
      ...prev,
      [activeTab]: clampPage(nextPage, activeList.length),
    }));
  }

  function handleDraftDeleted(requestId: string) {
    setItems((prev) => {
      const next = prev.filter((row) => row.id !== requestId);
      const nextDraftCount = next.filter((row) =>
        isLeaseInterestDraftStatus(row.status),
      ).length;
      setPageByTab((pages) => ({
        ...pages,
        drafts: clampPage(pages.drafts, nextDraftCount),
      }));
      return next;
    });
  }

  const emptyTabMessage =
    activeTab === "submitted"
      ? "No submitted requests yet. Complete and submit a draft to see it here."
      : "No drafts in progress. Start a request from a parcel to save a draft.";

  return (
    <>
      <PageHeading
        title="My preliminary interest requests"
        subtitle="Track drafts and preliminary assessment status. Submission does not reserve land."
      />
      {loading ? <LoadingState message="Loading requests…" /> : null}
      {error ? (
        <div className={styles.errorBlock} role="alert">
          <ErrorMessage message={error} />
          <Button
            type="button"
            variant="secondary"
            onClick={() => window.location.reload()}
          >
            Try again
          </Button>
        </div>
      ) : null}
      {!loading && !error && items.length === 0 ? (
        <EmptyState
          title="No requests yet"
          description="Express preliminary interest from a saved parcel or parcel intelligence record. Opening a parcel form alone does not create a draft."
          action={
            <Button href="/land-intelligence/parcels" variant="primary">
              Browse parcels
            </Button>
          }
        />
      ) : null}
      {!loading && !error && items.length > 0 ? (
        <div className={styles.workspace}>
          <div
            className={styles.tabList}
            role="tablist"
            aria-label="Request categories"
          >
            {MY_REQUESTS_TABS.map((tab) => {
              const selected = tab.value === activeTab;
              const count = tabLists[tab.value].length;
              return (
                <button
                  key={tab.value}
                  ref={(node) => {
                    tabRefs.current[tab.value] = node;
                  }}
                  type="button"
                  role="tab"
                  id={`my-requests-tab-${tab.value}`}
                  aria-selected={selected}
                  aria-controls={MY_REQUESTS_PANEL_ID}
                  tabIndex={selected ? 0 : -1}
                  className={`${styles.tab} ${selected ? styles.tabActive : ""}`}
                  onClick={() => selectTab(tab.value)}
                  onKeyDown={onTabKeyDown}
                >
                  {tab.label} ({count})
                </button>
              );
            })}
          </div>

          <div
            id={MY_REQUESTS_PANEL_ID}
            role="tabpanel"
            aria-labelledby={`my-requests-tab-${activeTab}`}
            className={styles.tabPanel}
          >
            {activeList.length === 0 ? (
              <p className={styles.sectionEmpty}>{emptyTabMessage}</p>
            ) : (
              <>
                <ul className={styles.list}>
                  {pageItems.map((item) => (
                    <RequestRow
                      key={item.id}
                      item={item}
                      onDeleted={
                        activeTab === "drafts" ? handleDraftDeleted : undefined
                      }
                    />
                  ))}
                </ul>
                {activeList.length > PAGE_SIZE ? (
                  <div className={styles.pager}>
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={activePage <= 1}
                      onClick={() => setActiveTabPage(activePage - 1)}
                    >
                      Previous
                    </Button>
                    <span className={styles.pagerLabel}>
                      Page {activePage} of {totalPages}
                    </span>
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={activePage >= totalPages}
                      onClick={() => setActiveTabPage(activePage + 1)}
                    >
                      Next
                    </Button>
                  </div>
                ) : null}
              </>
            )}
          </div>
        </div>
      ) : null}
    </>
  );
}

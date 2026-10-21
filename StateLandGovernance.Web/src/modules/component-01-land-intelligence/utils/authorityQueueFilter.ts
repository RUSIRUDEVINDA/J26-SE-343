/**
 * Pure helpers for the authority lease-interest queue tabs/filters.
 * Backend semantics: Pending = referred with valuation; Approved = approved in principle;
 * Rejected = declined after the valuation / authority path; All = union of the three.
 */

export type AuthorityQueueFilter = "Pending" | "Approved" | "Rejected" | "All";

export const DEFAULT_AUTHORITY_QUEUE_FILTER: AuthorityQueueFilter = "All";

export const AUTHORITY_QUEUE_FILTERS: {
  value: AuthorityQueueFilter;
  label: string;
}[] = [
  { value: "All", label: "All" },
  { value: "Pending", label: "Pending" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
];

export function parseAuthorityQueueFilter(
  value: string | null | undefined,
): AuthorityQueueFilter {
  const match = AUTHORITY_QUEUE_FILTERS.find(
    (option) => option.value.toLowerCase() === value?.trim().toLowerCase(),
  );
  return match?.value ?? DEFAULT_AUTHORITY_QUEUE_FILTER;
}

export function authorityQueueQueryString(
  page: number,
  pageSize: number,
  filter: AuthorityQueueFilter = DEFAULT_AUTHORITY_QUEUE_FILTER,
): string {
  const safePage = Number.isFinite(page) && page >= 1 ? Math.floor(page) : 1;
  const safeSize =
    Number.isFinite(pageSize) && pageSize >= 1 ? Math.floor(pageSize) : 20;
  return `page=${safePage}&pageSize=${safeSize}&statusFilter=${encodeURIComponent(
    parseAuthorityQueueFilter(filter),
  )}`;
}

export function authorityQueueEmptyMessage(filter: AuthorityQueueFilter): string {
  switch (filter) {
    case "Approved":
      return "No requests have been approved by the authority yet.";
    case "Rejected":
      return "No requests have been rejected after authority review yet.";
    case "All":
      return "No authority-reviewed or pending requests were found.";
    default:
      return "No referred requests with a saved officer valuation are waiting for authority review.";
  }
}

export function authorityQueueSummary(
  filter: AuthorityQueueFilter,
  totalCount: number,
  page: number,
  totalPages: number,
): string {
  if (totalCount === 0) {
    return authorityQueueEmptyMessage(filter);
  }
  const noun = `request${totalCount === 1 ? "" : "s"}`;
  const suffix = `(page ${page} of ${totalPages})`;
  switch (filter) {
    case "Approved":
      return `${totalCount} approved ${noun} ${suffix}.`;
    case "Rejected":
      return `${totalCount} rejected ${noun} ${suffix}.`;
    case "All":
      return `${totalCount} ${noun} ${suffix}.`;
    default:
      return `${totalCount} ${noun} ready for authority review ${suffix}.`;
  }
}

/** Pending cases are reviewed; completed cases open read-only. */
export function authorityQueueRowActionLabel(status: number): string {
  return status === 6 ? "Review" : "View";
}

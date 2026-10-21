import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import ts from "typescript";
import { createRequire } from "node:module";

const loadCommonJs = createRequire(import.meta.url);
loadCommonJs.extensions[".ts"] = (module, filename) =>
  module._compile(
    ts.transpileModule(fs.readFileSync(filename, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText,
    filename,
  );

const utils = "../src/modules/component-01-land-intelligence/utils";
const {
  AUTHORITY_QUEUE_FILTERS,
  DEFAULT_AUTHORITY_QUEUE_FILTER,
  authorityQueueEmptyMessage,
  authorityQueueQueryString,
  authorityQueueRowActionLabel,
  authorityQueueSummary,
  parseAuthorityQueueFilter,
} = loadCommonJs(`${utils}/authorityQueueFilter.ts`);
const {
  canRestoreFocusTo,
  nextTrappedFocusIndex,
  shouldCloseOnEscape,
} = loadCommonJs(`${utils}/modalDialogA11y.ts`);
const {
  isClosedLeaseInterestStatus,
  isValuationFormEditable,
  officerValuationPanelMode,
  selectReadOnlyValuation,
  unavailableValuationMessage,
} = loadCommonJs(`${utils}/officerClosedCase.ts`);

test("authority queue defaults to All and exposes all tabs", () => {
  assert.equal(DEFAULT_AUTHORITY_QUEUE_FILTER, "All");
  assert.deepEqual(
    AUTHORITY_QUEUE_FILTERS.map((f) => f.value),
    ["All", "Pending", "Approved", "Rejected"],
  );
});

test("parseAuthorityQueueFilter is case-insensitive and falls back to All", () => {
  assert.equal(parseAuthorityQueueFilter("approved"), "Approved");
  assert.equal(parseAuthorityQueueFilter(" REJECTED "), "Rejected");
  assert.equal(parseAuthorityQueueFilter("all"), "All");
  assert.equal(parseAuthorityQueueFilter("bogus"), "All");
  assert.equal(parseAuthorityQueueFilter(""), "All");
  assert.equal(parseAuthorityQueueFilter(null), "All");
  assert.equal(parseAuthorityQueueFilter(undefined), "All");
});

test("authorityQueueQueryString includes the status filter and sanitises paging", () => {
  assert.equal(
    authorityQueueQueryString(2, 20, "Approved"),
    "page=2&pageSize=20&statusFilter=Approved",
  );
  assert.equal(
    authorityQueueQueryString(0, -5),
    "page=1&pageSize=20&statusFilter=All",
  );
  assert.equal(
    authorityQueueQueryString(1, 20, "nonsense"),
    "page=1&pageSize=20&statusFilter=All",
  );
});

test("authority queue summary and empty messages differ per filter", () => {
  assert.match(authorityQueueEmptyMessage("Pending"), /waiting for authority review/);
  assert.match(authorityQueueEmptyMessage("Approved"), /approved/);
  assert.match(authorityQueueEmptyMessage("Rejected"), /rejected/);
  assert.equal(authorityQueueSummary("Approved", 0, 1, 1), authorityQueueEmptyMessage("Approved"));
  assert.equal(
    authorityQueueSummary("Pending", 1, 1, 1),
    "1 request ready for authority review (page 1 of 1).",
  );
  assert.equal(
    authorityQueueSummary("Rejected", 3, 1, 1),
    "3 rejected requests (page 1 of 1).",
  );
  assert.equal(authorityQueueSummary("All", 21, 2, 2), "21 requests (page 2 of 2).");
});

test("pending rows are reviewed while completed rows open read-only", () => {
  assert.equal(authorityQueueRowActionLabel(6), "Review");
  assert.equal(authorityQueueRowActionLabel(7), "View");
  assert.equal(authorityQueueRowActionLabel(5), "View");
});

test("nextTrappedFocusIndex wraps Tab and Shift+Tab inside the dialog", () => {
  assert.equal(nextTrappedFocusIndex(0, -1, false), null);
  assert.equal(nextTrappedFocusIndex(3, -1, false), 0);
  assert.equal(nextTrappedFocusIndex(3, -1, true), 2);
  assert.equal(nextTrappedFocusIndex(3, 0, false), 1);
  assert.equal(nextTrappedFocusIndex(3, 2, false), 0);
  assert.equal(nextTrappedFocusIndex(3, 0, true), 2);
  assert.equal(nextTrappedFocusIndex(3, 2, true), 1);
  assert.equal(nextTrappedFocusIndex(1, 0, false), 0);
  assert.equal(nextTrappedFocusIndex(1, 0, true), 0);
});

test("Escape closes the dialog only when not busy", () => {
  assert.equal(shouldCloseOnEscape("Escape", false), true);
  assert.equal(shouldCloseOnEscape("Escape", true), false);
  assert.equal(shouldCloseOnEscape("Tab", false), false);
});

test("focus is restored only to attached, focusable triggers", () => {
  assert.equal(canRestoreFocusTo(null), false);
  assert.equal(canRestoreFocusTo(undefined), false);
  assert.equal(canRestoreFocusTo({ isConnected: false, focus() {} }), false);
  assert.equal(canRestoreFocusTo({ isConnected: true, focus() {} }), true);
  assert.equal(canRestoreFocusTo({ isConnected: true }), false);
});

test("officer valuation form is editable only while referred to authority", () => {
  assert.equal(isValuationFormEditable(6), true);
  for (const status of [1, 2, 3, 4, 5, 7]) {
    assert.equal(isValuationFormEditable(status), false);
  }
  assert.equal(officerValuationPanelMode(6, 0), "editable");
  assert.equal(officerValuationPanelMode(6, 2), "editable");
  assert.equal(officerValuationPanelMode(7, 2), "readonly");
  assert.equal(officerValuationPanelMode(5, 1), "readonly");
  assert.equal(officerValuationPanelMode(5, 0), "unavailable");
  assert.equal(officerValuationPanelMode(3, 0), "unavailable");
});

test("closed statuses and unavailable messages", () => {
  assert.equal(isClosedLeaseInterestStatus(5), true);
  assert.equal(isClosedLeaseInterestStatus(7), true);
  assert.equal(isClosedLeaseInterestStatus(6), false);
  assert.match(unavailableValuationMessage(5), /closed/);
  assert.match(unavailableValuationMessage(3), /referred to the authority/);
});

test("selectReadOnlyValuation picks the chosen revision or the latest", () => {
  const rows = [{ id: "c", revision: 3 }, { id: "b", revision: 2 }, { id: "a", revision: 1 }];
  assert.equal(selectReadOnlyValuation(rows, "b").revision, 2);
  assert.equal(selectReadOnlyValuation(rows, null).revision, 3);
  assert.equal(selectReadOnlyValuation(rows, "missing").revision, 3);
  assert.equal(selectReadOnlyValuation([], "a"), null);
});

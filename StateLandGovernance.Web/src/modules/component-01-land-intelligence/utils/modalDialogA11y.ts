/**
 * Pure (DOM-free) helpers behind useModalDialogA11y so focus rules can be unit tested.
 */

export const FOCUSABLE_SELECTOR = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

/**
 * Index of the element that should receive focus when Tab / Shift+Tab is pressed
 * inside a modal dialog. Returns null when there is nothing focusable (the caller
 * should keep focus on the dialog container).
 *
 * @param count number of focusable elements inside the dialog
 * @param activeIndex index of the currently focused element, or -1 when focus is
 *   on the dialog container or outside it
 */
export function nextTrappedFocusIndex(
  count: number,
  activeIndex: number,
  shiftKey: boolean,
): number | null {
  if (count <= 0) {
    return null;
  }
  if (activeIndex < 0 || activeIndex >= count) {
    return shiftKey ? count - 1 : 0;
  }
  if (shiftKey) {
    return activeIndex === 0 ? count - 1 : activeIndex - 1;
  }
  return activeIndex === count - 1 ? 0 : activeIndex + 1;
}

/** Escape closes the dialog only when no save/submit is in flight. */
export function shouldCloseOnEscape(key: string, busy: boolean): boolean {
  return key === "Escape" && !busy;
}

/** Focus is restored only to a trigger that is still attached to the document. */
export function canRestoreFocusTo(
  element: { isConnected?: boolean; focus?: unknown } | null | undefined,
): boolean {
  return Boolean(
    element && element.isConnected !== false && typeof element.focus === "function",
  );
}

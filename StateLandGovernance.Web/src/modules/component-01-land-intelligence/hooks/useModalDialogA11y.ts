"use client";

import { useEffect, useRef, type RefObject } from "react";
import {
  canRestoreFocusTo,
  FOCUSABLE_SELECTOR,
  nextTrappedFocusIndex,
  shouldCloseOnEscape,
} from "../utils/modalDialogA11y";

/**
 * Basic modal-dialog accessibility:
 * - focuses the dialog container when it opens
 * - traps Tab / Shift+Tab inside the dialog
 * - closes on Escape unless `busy` (e.g. while saving)
 * - restores focus to the element that opened it when it closes
 *
 * Attach the returned ref to the element with role="dialog".
 */
export function useModalDialogA11y({
  open,
  onClose,
  busy = false,
}: {
  open: boolean;
  onClose: () => void;
  busy?: boolean;
}): { dialogRef: RefObject<HTMLDivElement | null> } {
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const onCloseRef = useRef(onClose);
  const busyRef = useRef(busy);

  useEffect(() => {
    onCloseRef.current = onClose;
    busyRef.current = busy;
  });

  useEffect(() => {
    if (!open) {
      return;
    }
    const dialog = dialogRef.current;
    const trigger =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;

    if (dialog) {
      dialog.tabIndex = -1;
      dialog.focus();
    }

    const onKeyDown = (event: KeyboardEvent) => {
      const container = dialogRef.current;
      if (!container) {
        return;
      }
      if (event.key === "Escape") {
        if (shouldCloseOnEscape(event.key, busyRef.current)) {
          event.preventDefault();
          onCloseRef.current();
        }
        return;
      }
      if (event.key !== "Tab") {
        return;
      }
      const focusable = Array.from(
        container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      );
      const activeIndex = focusable.indexOf(document.activeElement as HTMLElement);
      const target = nextTrappedFocusIndex(
        focusable.length,
        activeIndex,
        event.shiftKey,
      );
      event.preventDefault();
      (target == null ? container : focusable[target]).focus();
    };

    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      if (canRestoreFocusTo(trigger)) {
        trigger?.focus();
      }
    };
  }, [open]);

  return { dialogRef };
}

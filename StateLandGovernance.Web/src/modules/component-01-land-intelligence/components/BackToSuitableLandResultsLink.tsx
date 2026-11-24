"use client";

import { useMemo } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/components/Button";
import {
  confirmLeaveWithUnsavedChanges,
  resolveValidResultsReturn,
  suitableLandResultsFallbackHref,
} from "../utils/suitableLandResultsReturn";

/** PageHeading `action` — same Button pattern as Suitable Land Results “Edit search”. */
export function BackToSuitableLandResultsLink({
  hasUnsavedChanges = false,
}: {
  hasUnsavedChanges?: boolean;
}) {
  const searchParams = useSearchParams();
  const resultsHref = useMemo(
    () =>
      resolveValidResultsReturn(
        searchParams.get("resultsReturn"),
      ),
    [searchParams],
  );

  const href = resultsHref ?? suitableLandResultsFallbackHref();
  const label = resultsHref ? "Back to results" : "Find Suitable Land";

  return (
    <Button
      href={href}
      variant="secondary"
      onClick={(event) => {
        if (!confirmLeaveWithUnsavedChanges(hasUnsavedChanges)) {
          event.preventDefault();
        }
      }}
    >
      {label}
    </Button>
  );
}

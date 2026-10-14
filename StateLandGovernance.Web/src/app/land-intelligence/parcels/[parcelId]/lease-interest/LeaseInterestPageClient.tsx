"use client";

import { useState } from "react";
import { PageHeading } from "@/shared/components/PageLayout";
import { LeaseInterestFormView } from "@/modules/component-01-land-intelligence/components/LeaseInterestFormView";

export function LeaseInterestPageClient({ parcelId }: { parcelId: string }) {
  const [onReviewStep, setOnReviewStep] = useState(false);

  return (
    <>
      <PageHeading
        title={onReviewStep ? "Review preliminary interest" : "Express preliminary interest"}
        subtitle={
          onReviewStep
            ? "Check your details before submitting for preliminary assessment."
            : "Start a request only when ready. Viewing this page alone does not create a draft. Submission is preliminary verification only — not a reservation or approval."
        }
      />
      <LeaseInterestFormView
        parcelId={parcelId}
        onReviewStepChange={setOnReviewStep}
      />
    </>
  );
}

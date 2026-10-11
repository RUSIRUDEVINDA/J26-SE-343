"use client";

import { useState } from "react";
import { PageHeading } from "@/shared/components/PageLayout";
import { LeaseInterestFormView } from "@/modules/component-01-land-intelligence/components/LeaseInterestFormView";

export function LeaseInterestPageClient({ parcelId }: { parcelId: string }) {
  const [onReviewStep, setOnReviewStep] = useState(false);

  return (
    <>
      <PageHeading
        title={onReviewStep ? "Review lease interest" : "Express lease interest"}
        subtitle={
          onReviewStep
            ? "Check your details before submitting for preliminary assessment."
            : "Submit a preliminary interest request for officer verification. This is not a reservation or approval."
        }
      />
      <LeaseInterestFormView
        parcelId={parcelId}
        onReviewStepChange={setOnReviewStep}
      />
    </>
  );
}

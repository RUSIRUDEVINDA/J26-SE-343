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
            : "Review the parcel and the steps before starting your request."
        }
      />
      <LeaseInterestFormView
        parcelId={parcelId}
        onReviewStepChange={setOnReviewStep}
      />
    </>
  );
}

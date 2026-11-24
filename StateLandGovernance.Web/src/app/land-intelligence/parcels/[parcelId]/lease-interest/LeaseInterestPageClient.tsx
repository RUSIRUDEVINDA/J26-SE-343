"use client";

import { Suspense, useState } from "react";
import { PageHeading } from "@/shared/components/PageLayout";
import { BackToSuitableLandResultsLink } from "@/modules/component-01-land-intelligence/components/BackToSuitableLandResultsLink";
import { LeaseInterestFormView } from "@/modules/component-01-land-intelligence/components/LeaseInterestFormView";

function LeaseInterestPageContent({ parcelId }: { parcelId: string }) {
  const [onReviewStep, setOnReviewStep] = useState(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  return (
    <>
      <PageHeading
        title={onReviewStep ? "Review preliminary interest" : "Express preliminary interest"}
        subtitle={
          onReviewStep
            ? "Check your details before submitting for preliminary assessment."
            : "Review the parcel and the steps before starting your request."
        }
        action={
          <BackToSuitableLandResultsLink hasUnsavedChanges={hasUnsavedChanges} />
        }
      />
      <LeaseInterestFormView
        parcelId={parcelId}
        onReviewStepChange={setOnReviewStep}
        onDirtyChange={setHasUnsavedChanges}
      />
    </>
  );
}

export function LeaseInterestPageClient({ parcelId }: { parcelId: string }) {
  return (
    <Suspense fallback={null}>
      <LeaseInterestPageContent parcelId={parcelId} />
    </Suspense>
  );
}

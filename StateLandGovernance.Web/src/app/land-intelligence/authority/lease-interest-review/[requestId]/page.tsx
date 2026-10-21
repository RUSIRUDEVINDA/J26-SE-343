"use client";

import { useParams } from "next/navigation";
import { PageHeading } from "@/shared/components/PageLayout";
import { AuthorityLeaseInterestReviewView } from "@/modules/component-01-land-intelligence/components/AuthorityLeaseInterestReviewView";

export default function AuthorityLeaseInterestReviewPage() {
  const params = useParams<{ requestId: string }>();

  return (
    <>
      <PageHeading
        title="Authority decision"
        subtitle="Review the officer valuation and record a preliminary project decision."
      />
      <AuthorityLeaseInterestReviewView requestId={params.requestId} />
    </>
  );
}

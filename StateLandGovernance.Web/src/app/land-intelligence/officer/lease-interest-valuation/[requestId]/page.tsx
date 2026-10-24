import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestReviewView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestReviewView";

export default async function OfficerLeaseInterestValuationPage({
  params,
}: {
  params: Promise<{ requestId: string }>;
}) {
  const { requestId } = await params;
  return (
    <>
      <PageHeading
        title="Record officer valuation"
        subtitle="Preliminary worksheet only — a different officer from verification."
      />
      <OfficerLeaseInterestReviewView requestId={requestId} workflowMode="valuation" />
    </>
  );
}

import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestReviewView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestReviewView";

export default async function OfficerLeaseInterestViewPage({
  params,
}: {
  params: Promise<{ requestId: string }>;
}) {
  const { requestId } = await params;
  return (
    <>
      <PageHeading
        title="View request"
        subtitle="Read-only reference from officer applicant lookup. Use the verification or valuation queues to take action."
      />
      <OfficerLeaseInterestReviewView requestId={requestId} workflowMode="readonly" />
    </>
  );
}

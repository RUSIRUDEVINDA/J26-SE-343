import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestReviewView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestReviewView";

export default async function OfficerLeaseInterestReviewPage({
  params,
}: {
  params: Promise<{ requestId: string }>;
}) {
  const { requestId } = await params;
  return (
    <>
      <PageHeading
        title="Review lease interest"
        subtitle="Preliminary verification only."
      />
      <OfficerLeaseInterestReviewView requestId={requestId} workflowMode="verification" />
    </>
  );
}

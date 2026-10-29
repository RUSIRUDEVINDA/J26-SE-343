import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestQueueView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestQueueView";

export default function OfficerLeaseInterestValuationQueuePage() {
  return (
    <>
      <PageHeading
        title="Valuation queue"
        subtitle="Record one officer valuation per request after verification (different officer)."
      />
      <OfficerLeaseInterestQueueView queueKind="valuation" />
    </>
  );
}

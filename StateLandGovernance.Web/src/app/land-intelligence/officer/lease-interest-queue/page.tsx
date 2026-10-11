import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestQueueView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestQueueView";

export default function OfficerLeaseInterestQueuePage() {
  return (
    <>
      <PageHeading
        title="Verification queue"
        subtitle="Review submitted lease-interest requests — preliminary assessment only."
      />
      <OfficerLeaseInterestQueueView />
    </>
  );
}

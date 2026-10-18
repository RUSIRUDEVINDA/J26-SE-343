import { PageHeading } from "@/shared/components/PageLayout";
import { AuthorityLeaseInterestQueueView } from "@/modules/component-01-land-intelligence/components/AuthorityLeaseInterestQueueView";

export default function AuthorityLeaseInterestQueuePage() {
  return (
    <>
      <PageHeading
        title="Authority review queue"
        subtitle="Decide referred lease-interest requests after officer valuation — preliminary project outcomes only."
      />
      <AuthorityLeaseInterestQueueView />
    </>
  );
}

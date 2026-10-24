import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestLookupView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestLookupView";

export default function OfficerLeaseInterestLookupPage() {
  return (
    <>
      <PageHeading
        title="Officer applicant lookup"
        subtitle="Search and view submitted requests. Verification and valuation are handled from their queues."
      />
      <OfficerLeaseInterestLookupView />
    </>
  );
}

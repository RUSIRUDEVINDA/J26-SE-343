import { PageHeading } from "@/shared/components/PageLayout";
import { OfficerLeaseInterestLookupView } from "@/modules/component-01-land-intelligence/components/OfficerLeaseInterestLookupView";

export default function OfficerLeaseInterestLookupPage() {
  return (
    <>
      <PageHeading
        title="Lease interest lookup by NIC"
        subtitle=""
      />
      <OfficerLeaseInterestLookupView />
    </>
  );
}

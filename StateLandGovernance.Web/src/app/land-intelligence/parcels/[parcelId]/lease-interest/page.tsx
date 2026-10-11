import { PageHeading } from "@/shared/components/PageLayout";
import { LeaseInterestFormView } from "@/modules/component-01-land-intelligence/components/LeaseInterestFormView";

export default async function LeaseInterestPage({
  params,
}: {
  params: Promise<{ parcelId: string }>;
}) {
  const { parcelId } = await params;
  return (
    <>
      <PageHeading
        title="Express lease interest"
        subtitle="Submit a preliminary interest request for officer verification. This is not a reservation or approval."
      />
      <LeaseInterestFormView parcelId={parcelId} />
    </>
  );
}

import { LeaseInterestPageClient } from "./LeaseInterestPageClient";

export default async function LeaseInterestPage({
  params,
}: {
  params: Promise<{ parcelId: string }>;
}) {
  const { parcelId } = await params;
  return <LeaseInterestPageClient parcelId={parcelId} />;
}

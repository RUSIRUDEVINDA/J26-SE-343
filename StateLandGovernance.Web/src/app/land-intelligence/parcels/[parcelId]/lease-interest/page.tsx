import { Suspense } from "react";
import { LoadingState } from "@/shared/components/LoadingState";
import { LeaseInterestPageClient } from "./LeaseInterestPageClient";

export default async function LeaseInterestPage({
  params,
}: {
  params: Promise<{ parcelId: string }>;
}) {
  const { parcelId } = await params;
  return (
    <Suspense fallback={<LoadingState message="Loading…" />}>
      <LeaseInterestPageClient parcelId={parcelId} />
    </Suspense>
  );
}

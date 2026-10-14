import { Suspense } from "react";
import { LoadingState } from "@/shared/components/LoadingState";
import { ParcelIntelligenceView } from "../../parcel-intelligence/ParcelIntelligenceView";

export default async function ParcelByIdPage({
  params,
}: {
  params: Promise<{ parcelId: string }>;
}) {
  const { parcelId } = await params;
  return (
    <Suspense fallback={<LoadingState message="Loading…" />}>
      <ParcelIntelligenceView
        initialParcelId={parcelId}
        showCatalog={false}
      />
    </Suspense>
  );
}

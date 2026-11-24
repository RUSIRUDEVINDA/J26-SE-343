import { Suspense } from "react";
import { LoadingState } from "@/shared/components/LoadingState";
import { ParcelMapView } from "@/modules/component-01-land-intelligence/components/map/ParcelMapView";

export default async function ParcelMapPage({
  params,
}: {
  params: Promise<{ parcelId: string }>;
}) {
  const { parcelId } = await params;
  return (
    <Suspense fallback={<LoadingState message="Loading map…" />}>
      <ParcelMapView parcelId={parcelId} />
    </Suspense>
  );
}

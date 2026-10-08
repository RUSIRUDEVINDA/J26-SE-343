import { ParcelMapView } from "@/modules/component-01-land-intelligence/components/map/ParcelMapView";

export default async function ParcelMapPage({
  params,
}: {
  params: Promise<{ parcelId: string }>;
}) {
  const { parcelId } = await params;
  return <ParcelMapView parcelId={parcelId} />;
}

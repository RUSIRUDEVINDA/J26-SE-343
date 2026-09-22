import { Suspense } from "react";
import { LoadingState } from "@/shared/components/LoadingState";
import { ParcelIntelligenceView } from "../parcel-intelligence/ParcelIntelligenceView";

export default function ParcelsCatalogPage() {
  return (
    <Suspense fallback={<LoadingState message="Loading…" />}>
      <ParcelIntelligenceView showCatalog />
    </Suspense>
  );
}

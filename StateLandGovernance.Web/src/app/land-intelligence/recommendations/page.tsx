import { Suspense } from "react";
import { LoadingState } from "@/shared/components/LoadingState";
import { RecommendationsView } from "./RecommendationsView";

export default function RecommendationsPage() {
  return (
    <Suspense fallback={<LoadingState message="Loading recommendations…" />}>
      <RecommendationsView />
    </Suspense>
  );
}

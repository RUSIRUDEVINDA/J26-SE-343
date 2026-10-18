import { Suspense } from "react";
import { RecommendationsView } from "./RecommendationsView";

export default function RecommendationsPage() {
  return (
    <Suspense fallback={<p>Loading recommendation results…</p>}>
      <RecommendationsView />
    </Suspense>
  );
}

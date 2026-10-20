import { KnowledgeGraphView } from "./KnowledgeGraphView";

export default async function KnowledgeGraphPage({
  searchParams,
}: {
  searchParams: Promise<{ parcelId?: string }>;
}) {
  const params = await searchParams;
  const initialParcelId =
    typeof params.parcelId === "string" ? params.parcelId.trim() : "";

  return <KnowledgeGraphView initialParcelId={initialParcelId} />;
}

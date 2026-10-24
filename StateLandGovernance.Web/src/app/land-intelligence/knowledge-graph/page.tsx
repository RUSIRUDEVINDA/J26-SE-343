import { KnowledgeGraphView } from "./KnowledgeGraphView";

export default async function KnowledgeGraphPage({
  searchParams,
}: {
  searchParams: Promise<{ parcelId?: string; cadastralNumber?: string }>;
}) {
  const params = await searchParams;
  const initialParcelId =
    typeof params.parcelId === "string" ? params.parcelId.trim() : "";
  const initialCadastralNumber =
    typeof params.cadastralNumber === "string"
      ? params.cadastralNumber.trim()
      : "";

  return (
    <KnowledgeGraphView
      initialParcelId={initialParcelId}
      initialCadastralNumber={initialCadastralNumber}
    />
  );
}

import { redirect } from "next/navigation";

/** Legacy query-string route — prefer /land-intelligence/parcels/[parcelId]. */
export default async function ParcelIntelligenceLegacyPage({
  searchParams,
}: {
  searchParams: Promise<{ id?: string }>;
}) {
  const params = await searchParams;
  if (params.id?.trim()) {
    redirect(`/land-intelligence/parcels/${params.id.trim()}`);
  }
  redirect("/land-intelligence/parcels");
}

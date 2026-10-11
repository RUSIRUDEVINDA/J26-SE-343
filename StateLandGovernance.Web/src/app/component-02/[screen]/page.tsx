import { notFound } from "next/navigation";
import { FinancialScreen } from "@/modules/component-02/components/FinancialFlow";
import { screens, type Screen } from "@/modules/component-02/types/flow";
export default async function Component02ScreenPage({ params }: { params: Promise<{ screen: string }> }) {
  const { screen } = await params;
  if (!screens.includes(screen as Screen) || screen === "overview") notFound();
  return <FinancialScreen key={screen} screen={screen as Screen} />;
}

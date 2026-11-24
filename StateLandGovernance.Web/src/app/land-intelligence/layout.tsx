import type { ReactNode } from "react";
import { LandIntelligenceAuthGate } from "@/modules/component-01-land-intelligence/components/LandIntelligenceAuthGate";
import { ResearchDemoBanner } from "@/modules/component-01-land-intelligence/components/ResearchDemoBanner";

export default function Component01Layout({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <LandIntelligenceAuthGate>
      <ResearchDemoBanner />
      {children}
    </LandIntelligenceAuthGate>
  );
}

import type { ReactNode } from "react";
import { LandIntelligenceSessionBootstrap } from "@/modules/component-01-land-intelligence/components/LandIntelligenceSessionBootstrap";
import { ResearchDemoBanner } from "@/modules/component-01-land-intelligence/components/ResearchDemoBanner";
import { PageLayout } from "@/shared/components/PageLayout";

export default function Component01Layout({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <PageLayout showSidebar>
      <LandIntelligenceSessionBootstrap />
      <ResearchDemoBanner />
      {children}
    </PageLayout>
  );
}

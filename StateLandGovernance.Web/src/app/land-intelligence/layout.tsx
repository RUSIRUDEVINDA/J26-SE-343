import type { ReactNode } from "react";
import { ResearchDemoBanner } from "@/modules/component-01-land-intelligence/components/ResearchDemoBanner";
import { PageLayout } from "@/shared/components/PageLayout";

export default function Component01Layout({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <PageLayout showSidebar>
      <ResearchDemoBanner />
      {children}
    </PageLayout>
  );
}

import type { ReactNode } from "react";
import { PageLayout } from "@/shared/components/PageLayout";

export default function Component01Layout({
  children,
}: {
  children: ReactNode;
}) {
  return <PageLayout showSidebar>{children}</PageLayout>;
}

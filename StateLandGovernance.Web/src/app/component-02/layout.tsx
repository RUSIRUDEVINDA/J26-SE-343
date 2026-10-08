import type { ReactNode } from "react";
import { FinancialFlowLayout } from "@/modules/component-02/components/FinancialFlow";
export default function Component02Layout({ children }: { children: ReactNode }) {
  return <FinancialFlowLayout>{children}</FinancialFlowLayout>;
}

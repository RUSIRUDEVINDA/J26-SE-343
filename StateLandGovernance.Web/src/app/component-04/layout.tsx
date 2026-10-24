import type { ReactNode } from "react";
import { PageLayout } from "@/shared/components/PageLayout";
import { GovernanceWorkspace } from "@/modules/component-04/components/GovernanceWorkspace";

const navigation = [
  { label: "Overview", href: "/component-04", icon: "01" },
  { label: "Progressive screening", href: "/component-04/screening", icon: "02" },
  { label: "Assessment history", href: "/component-04/history", icon: "03" },
  { label: "Complaint intelligence", href: "/component-04/complaints", icon: "04" },
  { label: "Post-workflow analysis", href: "/component-04/post-workflow", icon: "05" },
  { label: "Governance follow-up", href: "/component-04/follow-up", icon: "06" },
  { label: "Integrity and audit", href: "/component-04/integrity", icon: "07" },
  { label: "Evaluation evidence", href: "/component-04/evaluation", icon: "08" },
  { label: "Development inputs", href: "/component-04/development", icon: "09" },
];

export default function Component04Layout({ children }: { children: ReactNode }) {
  return <PageLayout navigationItems={navigation} navigationLabel="Governance Intelligence navigation" navigationBasePath="/component-04" navigationCompactMobile><GovernanceWorkspace>{children}</GovernanceWorkspace></PageLayout>;
}

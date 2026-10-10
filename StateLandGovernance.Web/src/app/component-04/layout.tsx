import type { ReactNode } from "react";
import { PageLayout } from "@/shared/components/PageLayout";
import { GovernanceWorkspace } from "@/modules/component-04/components/GovernanceWorkspace";

const navigation = [
  { label: "Dashboard", href: "/component-04", icon: "▦" },
  { label: "Case assessments", href: "/component-04/cases", icon: "▤" },
  { label: "Complaint intelligence", href: "/component-04/complaints", icon: "◇" },
  { label: "Workflow anomalies", href: "/component-04/anomalies", icon: "◎" },
  { label: "Early screening", href: "/component-04/screening", icon: "▤" },
  { label: "Governance risk", href: "/component-04/risk", icon: "◇" },
  { label: "Explainability", href: "/component-04/explain", icon: "▤" },
  { label: "Institutional consensus", href: "/component-04/consensus", icon: "◇" },
  { label: "Condition verification", href: "/component-04/conditions", icon: "▤" },
  { label: "Lease approval timing", href: "/component-04/timing", icon: "◷" },
  { label: "Commissioner review", href: "/component-04/commissioner", icon: "◇" },
  { label: "Governance summary", href: "/component-04/summary", icon: "▤" },
  { label: "Audit history", href: "/component-04/audit", icon: "▤" },
  { label: "Verification record", href: "/component-04/verification", icon: "◎" },
];

export default function Component04Layout({ children }: { children: ReactNode }) {
  return <PageLayout navigationItems={navigation} navigationLabel="Governance Intelligence navigation" navigationBasePath="/component-04" navigationCompactMobile><GovernanceWorkspace>{children}</GovernanceWorkspace></PageLayout>;
}

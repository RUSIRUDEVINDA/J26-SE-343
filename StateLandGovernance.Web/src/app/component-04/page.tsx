import type { Metadata } from "next";
import { GovernanceDashboard } from "@/modules/component-04";

export const metadata: Metadata = { title: "Governance Intelligence", description: "Governance assessments and advisory evidence for state land lease review." };

export default function Component04Page() {
  return (
    <GovernanceDashboard />
  );
}

import { notFound } from "next/navigation";
import { CaseAssessments } from "@/modules/component-04/components/CaseAssessments";
import { CaseAssessmentDetail } from "@/modules/component-04/components/CaseAssessmentDetail";
import { GovernanceCapabilityPage, type Capability } from "@/modules/component-04/components/GovernanceCapabilityPage";

const capabilities = ["complaints", "anomalies", "screening", "timing", "commissioner", "summary", "verification", "audit", "risk", "explain", "consensus", "conditions"];
export default async function GovernancePage({ params }: { params: Promise<{ view: string[] }> }) {
  const { view } = await params;
  if (view.length === 1 && view[0] === "cases") return <CaseAssessments />;
  if (view.length === 2 && view[0] === "cases" && ["compliance", "conflicts"].includes(view[1])) return <CaseAssessmentDetail section={view[1]} />;
  if (view[0] === "cases" && view[1] === "assessment" && (view.length === 3 || (view.length === 4 && ["overview", "compliance", "conflicts"].includes(view[3])))) return <CaseAssessmentDetail assessmentId={view[2]} section={view[3]} />;
  if (view.length === 2 && view[0] === "commissioner" && view[1] === "review") return <GovernanceCapabilityPage capability="decision-review" />;
  if (view.length === 1 && capabilities.includes(view[0])) return <GovernanceCapabilityPage capability={view[0] as Capability} />;
  notFound();
}

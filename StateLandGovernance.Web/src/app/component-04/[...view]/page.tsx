import {RegisteredComplaints} from "@/modules/component-04/components/RegisteredComplaints";
import {PostWorkflowDemonstrations} from "@/modules/component-04/components/PostWorkflowDemonstrations";
import { IndependentRegulatoryReports } from "@/modules/component-04/components/IndependentRegulatoryReports";
import { EvaluationEvidence } from "@/modules/component-04/components/EvaluationEvidence";
import { IndependentGovernance, IndependentScreeningCase } from "@/modules/component-04/components/IndependentGovernance";
import { notFound } from "next/navigation";
import { CaseScreeningQueue } from "@/modules/component-04/components/CaseScreeningQueue";
import { GovernanceCapabilityPage, type Capability } from "@/modules/component-04/components/GovernanceCapabilityPage";

const capabilities = ["complaints", "anomalies", "screening", "timing", "commissioner", "summary", "verification", "audit", "risk", "explain", "consensus", "conditions"];
export default async function GovernancePage({ params, searchParams }: { params: Promise<{ view: string[] }>; searchParams: Promise<{ snapshotId?: string }> }) {
  const { view } = await params;
  if(view[0]==="post-workflow"&&view.length===1)return <PostWorkflowDemonstrations/>;
  if(view[0]==="post-workflow"&&view[1]==="reports"&&view.length===3)return <PostWorkflowDemonstrations id={view[2]}/>;
  if(view[0]==="regulatory-assessments"&&(view.length===1||view.length===2)) return <IndependentRegulatoryReports id={view[1]}/>;
  if(view.length===1&&view[0]==="complaints")return <RegisteredComplaints officer/>;
  if(view.length===1&&view[0]==="complaint-sandbox")return <IndependentGovernance screen="complaints"/>;
  if(view.length===1&&view[0]==="evaluation")return <EvaluationEvidence/>;
  const independent=["development","complaints","post-workflow","follow-up","integrity","history"] as const;
  if(view.length===1&&independent.some(x=>x===view[0]))return <IndependentGovernance screen={view[0] as typeof independent[number]}/>;
  if(view.length===1&&view[0]==="anomalies")return <IndependentGovernance screen="post-workflow"/>;
  if(view.length===1&&["verification","audit"].includes(view[0]))return <IndependentGovernance screen="integrity"/>;
  if(view.length===1&&view[0]==="cases")return <IndependentGovernance screen="history"/>;
  if (view.length === 1 && view[0] === "screening") return <CaseScreeningQueue />;
  if (view.length === 1 && view[0] === "screening-evidence") return <GovernanceCapabilityPage capability="screening" />;
  if (view.length === 3 && view[0] === "screening" && view[1] === "cases") return <IndependentScreeningCase caseId={view[2]} snapshotId={(await searchParams).snapshotId} />;

  if (view.length === 2 && view[0] === "cases" && ["compliance", "conflicts"].includes(view[1])) return <IndependentGovernance screen="history"/>;
  if (view[0] === "cases" && view[1] === "assessment" && (view.length === 3 || (view.length === 4 && ["overview", "compliance", "conflicts"].includes(view[3])))) return <IndependentGovernance screen="history"/>;
  if (view.length === 2 && view[0] === "commissioner" && view[1] === "review") return <GovernanceCapabilityPage capability="decision-review" />;
  if (view.length === 1 && capabilities.includes(view[0])) return <GovernanceCapabilityPage capability={view[0] as Capability} />;
  notFound();
}

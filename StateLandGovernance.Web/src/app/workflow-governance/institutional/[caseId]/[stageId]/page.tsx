"use client";
import { useParams } from "next/navigation";
import { InstitutionalRequestWorkspace } from "@/modules/workflow-governance/components/WorkflowExecutionWorkspace";
export default function InstitutionalRequestPage() {
  const { caseId, stageId } = useParams<{ caseId: string; stageId: string }>();
  return <InstitutionalRequestWorkspace caseId={caseId} stageId={stageId} />;
}

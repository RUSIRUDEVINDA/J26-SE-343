import {apiGet, ApiClientError} from "@/lib/apiClient";
import {readDevelopmentSession} from "@/lib/developmentSession";
export type RegulatoryFinding = {ruleCode:string;ruleVersion:string;category:string;applicability:string;status:string;
 severity:string;isBlocking:boolean;requiresHumanReview:boolean;observedValueSummary:string;expectedRequirement:string;
 evidenceStatus:string;calculationStatus:string;sourceAuthority:string;sourceDocument:string;sourceSection:string;recommendedAction:string};
export type RegulatorySummary = {id:string;auditRecordId:string;caseId:string|null;proposalId:string|null;actionName:string;
 status:string;evaluationTimestamp:string;ruleSetVersion:string;deterministicEvaluationId:string|null};
export type RegulatoryReport = {metadata:RegulatorySummary;findings:RegulatoryFinding[]|null;provenance?:{earlyRequestId?:string|null;creatorActorId:string;submissionKey:string;evaluatedInputHash:string;inputVersion:string;schemaVersion:string;sourceReference:string;catalogueDigest:string;catalogueVersion:string;inputJson:string;c3Binding:unknown}|null};
export const regulatoryGroups=["Issues","Unresolved","Human review","Not applicable","Satisfied checks","Other returned findings"] as const;
export function regulatoryGroup(f:RegulatoryFinding):typeof regulatoryGroups[number]{
 if(f.isBlocking)return "Issues";
 if(f.status==="InsufficientInformation")return "Unresolved";
 if(f.applicability==="NotApplicable"||f.status==="NotApplicable")return "Not applicable";
 if(f.requiresHumanReview||["RequiresHumanReview","RequiresReview"].includes(f.status))return "Human review";
 if(f.status==="NonCompliant")return "Issues";
 return f.status==="Compliant"?"Satisfied checks":"Other returned findings";
}
export function regulatoryCounts(findings:readonly RegulatoryFinding[]){
 return findings.reduce<Record<string,number>>((counts,f)=>{counts[f.status]=(counts[f.status]??0)+1;return counts;},{});
}
export function filterRegulatoryFindings(findings:readonly RegulatoryFinding[],status:string,query:string){
 const term=query.trim().toLocaleLowerCase();
 return findings.filter(f=>(!status||f.status===status)&&(!term||[f.ruleCode,f.observedValueSummary,f.sourceAuthority,f.sourceDocument,f.sourceSection].some(v=>(v??"").toLocaleLowerCase().includes(term))));
}
export function regulatoryError(error:unknown){
 if(error instanceof ApiClientError){if(error.status===401)return "Sign in to read your assigned reports.";
 if(error.status===403)return "Access denied. An active assignment to this exact report is required.";
 if(error.status===404)return "The assigned report was not found.";}
 return error instanceof Error?error.message:"The report could not be loaded.";
}
async function read<T>(path:string,signal?:AbortSignal):Promise<T>{
 // Existing portal session only. Never mint a Component1 arbitrary-profile token as a fallback.
 if(!readDevelopmentSession())throw new ApiClientError({status:401,title:"Sign in required",detail:"Sign in to continue."});
 return apiGet<T>(path,{signal,cache:"no-store"},"none");
}
export const listRegulatoryReports=(signal?:AbortSignal)=>read<RegulatorySummary[]>("/api/governance-intelligence/regulatory-assessments?limit=100",signal);
export async function getRegulatoryReport(id:string,signal?:AbortSignal){
 const value=await read<RegulatoryReport>(`/api/governance-intelligence/regulatory-assessments/${encodeURIComponent(id)}`,signal);
 if(value?.metadata?.id!==id||!(value.findings===null||Array.isArray(value.findings)))throw new Error("Stored report response is invalid; no findings displayed.");
 return value;
}

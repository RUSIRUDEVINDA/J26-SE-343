import type {ConsolidatedAssessment} from "@/modules/workflow-governance/types/caseScreening";
import {apiGet} from "@/lib/apiClient";
export type ConsolidatedHistoryItem=ConsolidatedAssessment & {assessmentId:string;caseId:string;verifiedFactSnapshotId:string;proposalVersion:string;evaluatedAtUtc:string;overallStatus:string;officerRecommendationSummary:string;fullInputFingerprint?:string;effectivePolicyVersion?:string;evidenceVersion?:string;provenance?:{isDevelopmentFixture?:boolean;fixtureLabel?:string;regulatoryCatalogueSource?:string};};
export async function consolidatedHistory(caseId:string){const rows=await apiGet<ConsolidatedHistoryItem[]>(`/api/governance-intelligence/screening/cases/${encodeURIComponent(caseId)}/history-consolidated?limit=30`,undefined,"officer");if(!Array.isArray(rows)||rows.some(x=>x.caseId!==caseId))throw new Error("History response does not match the requested assigned case.");return rows;}
export async function syntheticCaseAccess(caseId:string){
 try{return await apiGet<{available:boolean;canDesignate?:boolean}>(`/api/development-auth/cases/${caseId}/screening-scenarios`,undefined,"officer");}
 catch{return apiGet<{available:boolean}>(`/api/development-auth/screening-scenarios?caseId=${caseId}`,undefined,"officer");}
}
export function localInputPanelAllowed(environment:string,hostname:string,serverEnabled:boolean){return environment==="development"&&["127.0.0.1","localhost"].includes(hostname)&&serverEnabled;}
export function matchesCurrentAssessment(item:ConsolidatedHistoryItem,current:{isCurrent:boolean;verifiedFactSnapshotId?:string;latestAssessment?:{assessment?:{assessmentId?:string}}}|null){return Boolean(current?.isCurrent&&current.latestAssessment?.assessment?.assessmentId===item.assessmentId&&current.verifiedFactSnapshotId===item.verifiedFactSnapshotId);}

export function requireMatchingEngineCase<T extends {caseId:string}>(result:T,caseId:string):T{if(result.caseId!==caseId)throw new Error("Engine response belongs to another case. No result is displayed.");return result;}

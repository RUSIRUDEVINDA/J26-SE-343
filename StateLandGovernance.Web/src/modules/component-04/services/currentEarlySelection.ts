import type {RegulatoryReport} from "./independentRegulatoryReports";
import type {Investigation,RequestReport} from "./officerFlows";
const key=(actor:string)=>`c4-current-early/1/${actor}`;
export function rememberCurrentEarly(actor:string,id:string|null,storage:Pick<Storage,"setItem"|"removeItem">){
 if(id)storage.setItem(key(actor),id);else storage.removeItem(key(actor));
}
export function selectedCurrentEarly(actor:string,query:URLSearchParams,storage:Pick<Storage,"getItem">){
 if(query.has("freshWorkflow"))return null;
 return query.get("earlyRequestId")||storage.getItem(key(actor));
}
export function clearWorkflowSelection(actor:string,storage:Pick<Storage,"removeItem">){
 storage.removeItem(key(actor));storage.removeItem(`c4-step1-selected/1/${actor}`);storage.removeItem(`c4-active-workflow/1/${actor}`);
}
export type ActiveWorkflow={earlyRequestId:string|null;regulatoryEvaluationId:string|null;conflictReportId:string|null;proposalId?:string;investigationId?:string;earlyAssessmentId?:string};
export function rememberActiveWorkflow(actor:string,value:ActiveWorkflow,storage:Pick<Storage,"setItem">){storage.setItem(`c4-active-workflow/1/${actor}`,JSON.stringify(value));}
export function readActiveWorkflow(actor:string,q:URLSearchParams,storage:Pick<Storage,"getItem">):ActiveWorkflow{
 const empty={earlyRequestId:null,regulatoryEvaluationId:null,conflictReportId:null};if(q.has("freshWorkflow"))return empty;
 if(["earlyRequestId","regulatoryEvaluationId","conflictReportId"].some(k=>q.has(k)))return {earlyRequestId:q.get("earlyRequestId"),regulatoryEvaluationId:q.get("regulatoryEvaluationId"),conflictReportId:q.get("conflictReportId")};
 try{const value=JSON.parse(storage.getItem(`c4-active-workflow/1/${actor}`)||"null");if(value&&[value.earlyRequestId,value.regulatoryEvaluationId,value.conflictReportId].every(x=>x===null||typeof x==="string"))return value;}catch{}
 return {...empty,earlyRequestId:selectedCurrentEarly(actor,q,storage)};
}
export function matchesCurrentEarly(id:string|null,report:RequestReport|null){return !!id&&report?.id===id&&report.kind==="early";}

// Persist only identities confirmed by authorized reads, never unvalidated URL values.
export function validatedActiveWorkflow(actor:string,selection:ActiveWorkflow,early:RequestReport,investigation:Investigation,regulatory:RegulatoryReport|null,proposalId:string):ActiveWorkflow{
 if(!actor||early.creatorActorId!==actor||early.id!==selection.earlyRequestId||early.kind!=="early"||early.state!=="Saved"||!early.earlyAssessmentId||!early.investigationId||investigation.id!==early.investigationId||investigation.proposalId!==proposalId)throw new Error("The selected saved workflow is unavailable or does not belong to this officer and proposal.");
 if(selection.regulatoryEvaluationId&&(!regulatory||regulatory.metadata.id!==selection.regulatoryEvaluationId||regulatory.metadata.proposalId!==proposalId||regulatory.provenance?.creatorActorId!==actor||regulatory.provenance.earlyRequestId!==early.id))throw new Error("The selected Regulatory evaluation does not belong to the exact active Early workflow.");
 return {...selection,proposalId,investigationId:investigation.id,earlyAssessmentId:early.earlyAssessmentId};
}

export function currentRegulatoryForEarly(report:RegulatoryReport|null,earlyId:string|null,evaluationId:string|null){
 return report&&earlyId&&evaluationId&&report.metadata.id===evaluationId&&report.provenance?.earlyRequestId===earlyId?report:null;
}

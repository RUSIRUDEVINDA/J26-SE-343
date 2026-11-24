import {apiGet,apiPost,ApiClientError} from "@/lib/apiClient";
import {readDevelopmentSession} from "@/lib/developmentSession";
import type {RegulatoryReport} from "./independentRegulatoryReports";
export type JsonValue=null|boolean|number|string|JsonValue[]|{[key:string]:JsonValue};
export type RegulatoryInput={[key:string]:JsonValue};
export type RegulatoryRequest={earlyRequestId?:string|null;actionName:string;input:RegulatoryInput;submissionKey:string;inputVersion:string;schemaVersion:string;sourceReference:string;c3Binding:null|{caseId:string;snapshotId:string|null;proposalVersion:string|null}};
export type RegulatoryRun={report:RegulatoryReport;reused:boolean};
export function validateRegulatoryDraft(input:RegulatoryInput,version:string,source:string){
 const errors:string[]=[];if(!input||Array.isArray(input)||typeof input.proposalId!=="string"||!input.proposalId.trim())errors.push("A proposal identifier is required.");
 if(!version.trim())errors.push("An input version is required.");if(!source.trim())errors.push("A source and assumption description is required.");
 function visit(v:JsonValue){if(typeof v==="number"&&!Number.isFinite(v))errors.push("Numbers must be finite.");else if(Array.isArray(v))v.forEach(visit);else if(v&&typeof v==="object")Object.values(v).forEach(visit);}
 visit(input);return errors;
}
function storageKey(){const session=readDevelopmentSession();if(!session)throw new Error("Sign in as an officer.");return "regulatory-pending:"+session.user.actorId;}
export function workflowPendingKey(actor:string,early:string|null|undefined){return `regulatory-pending:${actor}:early:${encodeURIComponent(early??"unbound")}`;}
export function savePending(request:RegulatoryRequest,earlyScope?:string){const actor=readDevelopmentSession()?.user.actorId;if(!actor)throw new Error("Sign in as an officer.");sessionStorage.setItem(earlyScope===undefined?storageKey():workflowPendingKey(actor,earlyScope),JSON.stringify(request));}
export function pendingRequest(early?:string|null):RegulatoryRequest|null{
 const actor=readDevelopmentSession()?.user.actorId;if(!actor)throw new Error("Sign in as an officer.");
 if(early===undefined){const value=sessionStorage.getItem(storageKey());return value?JSON.parse(value):null;}
 const value=sessionStorage.getItem(workflowPendingKey(actor,early));if(value){const request=JSON.parse(value) as RegulatoryRequest;if(request.earlyRequestId!==early)throw new Error("Pending request does not match this Early workflow.");return request;}
 // Keep older uncertain submissions intact; only an exact Early binding may resume one.
 const legacy=sessionStorage.getItem(storageKey());if(legacy){const request=JSON.parse(legacy) as RegulatoryRequest;if(request.earlyRequestId===early)return request;}return null;
}
export function clearPending(early?:string|null){const actor=readDevelopmentSession()?.user.actorId;if(!actor)throw new Error("Sign in as an officer.");if(early===undefined){sessionStorage.removeItem(storageKey());return;}sessionStorage.removeItem(workflowPendingKey(actor,early));const legacy=sessionStorage.getItem(storageKey());if(legacy&&(JSON.parse(legacy) as RegulatoryRequest).earlyRequestId===early)sessionStorage.removeItem(storageKey());}
export function requireSavedRegulatoryReport(value:RegulatoryReport){
 const actor=readDevelopmentSession()?.user.actorId;
 if(!value?.metadata?.id||!(value.findings===null||Array.isArray(value.findings))||!value.provenance||value.provenance.creatorActorId!==actor)throw new Error("Saved report was not confirmed for this session. Keep the submission key and check its status.");
 return value;
}
export function requireActiveRegulatoryBinding(value:RegulatoryReport,earlyRequestId:string|undefined){
 if(!earlyRequestId||value.provenance?.earlyRequestId!==earlyRequestId)throw new Error("Saved Regulatory evaluation does not belong to the exact active Early request. Retain the pending submission key and reconcile it separately.");
 return value;
}
export const runRegulatory=async(request:RegulatoryRequest)=>{storageKey();const value=await apiPost<RegulatoryRun>("/api/governance-intelligence/evaluate-compliance",request,undefined,"none");requireSavedRegulatoryReport(value?.report);if(typeof value.reused!=="boolean")throw new Error("Submission disposition is missing; check saved submission.");return value;};
export async function findRegulatorySubmission(key:string){storageKey();try{return requireSavedRegulatoryReport(await apiGet<RegulatoryReport>(`/api/governance-intelligence/regulatory-assessments/submissions/${encodeURIComponent(key)}`,{cache:"no-store"},"none"));}catch(e){if(e instanceof ApiClientError&&e.status===404)return null;throw e;}}

export async function regulatorySubmissionEnabled(){storageKey();const value=await apiGet<{enabled?:boolean;schemaVersion?:string}>("/api/governance-intelligence/regulatory-assessments/capabilities",{cache:"no-store"},"none");return value?.enabled===true&&value.schemaVersion==="c4-regulatory-input/1";}

export function isCorrectableRegulatoryError(error:unknown){return error instanceof ApiClientError&&(error.status===400||error.status===403);}
export function regulatorySubmissionKey(previous:RegulatoryRequest|null){return previous?.submissionKey??crypto.randomUUID();}

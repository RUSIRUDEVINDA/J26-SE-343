import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
import {C4RequestError,confirmedJson} from "./requestOutcome";
import type {Investigation} from "./officerFlows";
export type DraftInvestigation=Investigation&{inputSha256:string};
export type InvestigationFile={id:string;investigationId:string;concern:string;actorId:string;createdAtUtc:string;fileName:string;contentType:string;bytes:number;sha256:string};
export type InvestigationCompletion={id:string;investigationId:string;actorId:string;createdAtUtc:string;submissionKey:string;inputSha256:string;eventSha256:string};
export type InvestigationEvidence={canWrite:boolean;attachments:InvestigationFile[];finalization:InvestigationCompletion|null};
export function canFinalizeInvestigation(findings:Investigation["findings"],files:InvestigationFile[]){return findings.length===7&&new Set(findings.map(f=>f.concern)).size===7&&findings.every(f=>f.choice==="NoIssueFound"||(f.choice==="IssueFound"&&f.remarks.trim().length>=20&&f.remarks.trim().split(/\s+/).length>=3&&files.some(a=>a.concern===f.concern)));}
async function request<T>(id:string,suffix:string,body?:FormData|object):Promise<T>{
 const session=readDevelopmentSession();if(!session)throw new C4RequestError("Sign in again.","rejected",401);let response:Response;
 try{response=await fetch(`${developmentApiBase()}/api/governance-intelligence/officer-investigations/${encodeURIComponent(id)}/evidence${suffix}`,{cache:"no-store",method:body?"POST":"GET",signal:AbortSignal.timeout(body?60000:20000),headers:{Authorization:`Bearer ${session.accessToken}`,...(body&&!(body instanceof FormData)?{"Content-Type":"application/json"}:{})},body:body instanceof FormData?body:body?JSON.stringify(body):undefined});}catch{throw new C4RequestError("Response unavailable. Refresh saved evidence before replaying the same submission.",body?"uncertain":"rejected");}
 return confirmedJson<T>(response,!!body);
}
export const investigationEvidence={
 read:(id:string)=>request<InvestigationEvidence>(id,""),
 attach:(id:string,body:FormData)=>request<InvestigationFile>(id,"/attachments",body),
 finalize:(id:string,body:{submissionKey:string;expectedInputSha256:string})=>request<InvestigationCompletion>(id,"/finalize",body),
 download:async(id:string,file:InvestigationFile)=>{
  const session=readDevelopmentSession();if(!session)throw new Error("Sign in again.");const response=await fetch(`${developmentApiBase()}/api/governance-intelligence/officer-investigations/${encodeURIComponent(id)}/evidence/attachments/${encodeURIComponent(file.id)}`,{cache:"no-store",headers:{Authorization:`Bearer ${session.accessToken}`}});
  if(!response.ok){await confirmedJson(response,false);return;}const blob=await response.blob();if(readDevelopmentSession()?.accessToken!==session.accessToken)return;const url=URL.createObjectURL(blob);const a=document.createElement("a");a.href=url;a.download=file.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
 }
};

import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
import {C4RequestError,confirmedJson} from "./requestOutcome";
export type EvidenceAttachment={id:string;requestId:string;concern:string;actorId:string;createdAtUtc:string;fileName:string;contentType:string;bytes:number;sha256:string};
export type Verification={id:string;requestId:string;concern:string;revision:number;previousStatus:string;status:string;actorId:string;createdAtUtc:string;remarks:string;attachmentIds:string[];evidenceReferences:string[];eventSha256:string};
export type EvidenceView={canVerify:boolean;attachments:EvidenceAttachment[];history:Verification[]};
export type PreviousEvidence={requestId:string;assessmentId:string;createdAtUtc:string;evidence:EvidenceView};
export type PreviousEvidencePage={items:PreviousEvidence[];nextCursor:string|null};
export type VerificationInput={submissionKey:string;concern:string;expectedRevision:number;decision:string;remarks:string;attachmentIds:string[];evidenceReferences:string[]};
async function request<T>(id:string,suffix:string,body?:FormData|VerificationInput,waiting?:()=>void):Promise<T>{
 const session=readDevelopmentSession();if(!session)throw new C4RequestError("Sign in again.","rejected",401);
 const form=body instanceof FormData;let response:Response;
 try{response=await fetch(`${developmentApiBase()}/api/governance-intelligence/officer-assessments/${encodeURIComponent(id)}/manual-evidence${suffix}`,{signal:AbortSignal.timeout(body?60000:20000),method:body?"POST":"GET",cache:"no-store",headers:{Authorization:`Bearer ${session.accessToken}`,...(body&&!form?{"Content-Type":"application/json"}:{})},body:body?(form?body:JSON.stringify(body)):undefined});}
 catch{throw new C4RequestError(body?"Network response lost; refresh saved history before replaying the same request.":"Could not refresh saved evidence.",body?"uncertain":"rejected");}
 if(response.ok)waiting?.();return confirmedJson<T>(response,!!body);
}
export const manualEvidence={
 read:(id:string)=>request<EvidenceView>(id,""),
 previous:(id:string,cursor?:string)=>request<PreviousEvidencePage>(id,`/previous?limit=5${cursor?`&cursor=${encodeURIComponent(cursor)}`:""}`),
 attach:(id:string,form:FormData,waiting?:()=>void)=>request<EvidenceAttachment>(id,"/attachments",form,waiting),
 verify:(id:string,body:VerificationInput)=>request<Verification>(id,"/verifications",body),
 download:async(id:string,attachment:EvidenceAttachment)=>{
  const session=readDevelopmentSession();if(!session)throw new Error("Sign in again.");
  const response=await fetch(`${developmentApiBase()}/api/governance-intelligence/officer-assessments/${encodeURIComponent(id)}/manual-evidence/attachments/${encodeURIComponent(attachment.id)}`,{cache:"no-store",headers:{Authorization:`Bearer ${session.accessToken}`}});
  if(!response.ok){await confirmedJson(response,false);return;}
  const blob=await response.blob();if(readDevelopmentSession()?.accessToken!==session.accessToken)return;
  const url=URL.createObjectURL(blob);const a=document.createElement("a");a.href=url;a.download=attachment.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
 }
};

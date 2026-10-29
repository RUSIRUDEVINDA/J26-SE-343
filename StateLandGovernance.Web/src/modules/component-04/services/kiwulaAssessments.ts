import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
export type KiwulaEngine="conflict"|"early";
export type KiwulaReport={id:string;creatorActorId:string;submissionKey:string;engine:KiwulaEngine;proposalId:string;inputVersion:string;inputHash:string;engineVersion:string;sourceReference:string;engineArtifactHash:string;inputJson:string;createdAtUtc:string;assessmentId:string;auditRecordId:string|null;input:Record<string,unknown>[];result:{status?:string;overallStatus?:string;hasIncompleteEvidence?:boolean;conflicts?:{conflictId:string;conflictType:string;severity:string;involvedDecisionIds:string[];explanation:string;evidenceRule:string;recommendedAction:string}[];indicatorResults?:{indicatorType:string;evidenceState:string;message:string;requiresReview:boolean;needsEvidence:boolean;evidenceReference:string|null}[]}};
export type KiwulaCatalogue={canRun:boolean;proposal:{displayName:string;originalName:string;version:string;proposalId:string;source:string;location:Record<string,string>;extent:string;proposedUse:string;tenure:string;unknowns:string[];routingSource:string;workflow:{code:string;institution:string;status:string;dependsOn:string;reason:string}[];decisions:Record<string,unknown>[];indicators:Record<string,unknown>[];workflowScenario:string}};
export function kiwulaMessage(status:number){return status===401?"Sign in with your officer account.":status===403?"This account cannot access this assessment.":status===404?"The report or updated API endpoint is unavailable.":status===409?"This submission key belongs to a different input. Retrieve its saved report.":status===503?"The Kiwula workspace requires controlled activation or storage is unavailable.":"Request failed. Refresh saved history before replaying the same submission.";}
async function request<T>(suffix:string,signal?:AbortSignal,data?:unknown):Promise<T>{
 const session=readDevelopmentSession();if(!session)throw new Error(kiwulaMessage(401));
 const response=await fetch(developmentApiBase()+"/api/governance-intelligence/kiwula-assessments"+suffix,{method:data===undefined?"GET":"POST",cache:"no-store",signal,headers:{Authorization:`Bearer ${session.accessToken}`,...(data===undefined?{}:{"Content-Type":"application/json"})},body:data===undefined?undefined:JSON.stringify(data)});
 if(!response.ok)throw new Error(kiwulaMessage(response.status));return response.json() as Promise<T>;
}
export function stableKiwulaKey(actor:string,engine:KiwulaEngine,storage:Pick<Storage,"getItem"|"setItem">,make:()=>string){const name=`c4-kiwula/1/${actor}/${engine}`;const old=storage.getItem(name);if(old)return old;const key=make();storage.setItem(name,key);return key;}
export const kiwulaApi={catalogue:(signal?:AbortSignal)=>request<KiwulaCatalogue>("/catalogue",signal),history:(signal?:AbortSignal)=>request<KiwulaReport[]>("",signal),get:(id:string,signal?:AbortSignal)=>request<KiwulaReport>(`/${encodeURIComponent(id)}`,signal),run:(engine:KiwulaEngine,submissionKey:string,signal?:AbortSignal)=>request<KiwulaReport>("",signal,{engine,submissionKey})};

/** Only reuse saved results for the selected proposal and current server input version. */
export function savedKiwulaAssessment(rows:readonly KiwulaReport[],engine:string,proposalId:string,inputVersion:string){
 return rows.filter(r=>r.engine===engine&&r.proposalId===proposalId&&r.inputVersion===inputVersion)
  .sort((a,b)=>b.createdAtUtc.localeCompare(a.createdAtUtc))[0];
}

export function deliberateKiwulaKey(actor:string,engine:KiwulaEngine,storage:Pick<Storage,"getItem"|"setItem">,make:()=>string){
 const pending=`c4-kiwula-pending/1/${actor}/${engine}`;const old=storage.getItem(pending);if(old)return old;
 const key=make();storage.setItem(pending,key);storage.setItem(`c4-kiwula/1/${actor}/${engine}`,key);return key;
}
export function completeKiwulaSubmission(actor:string,engine:KiwulaEngine,storage:Pick<Storage,"removeItem">){storage.removeItem(`c4-kiwula-pending/1/${actor}/${engine}`);}

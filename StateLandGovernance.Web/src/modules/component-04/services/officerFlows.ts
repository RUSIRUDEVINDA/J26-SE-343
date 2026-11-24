import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
import type {InvestigationChoice} from "./earlyInvestigation";
export type Finding={concern:string;choice:InvestigationChoice;remarks:string};
export type Investigation={id:string;proposalId:string;sourceVersion:string;findings:Finding[];createdAtUtc:string;supersedesId:string|null};
export type Indicator={indicatorType:string;evidenceState:string;message:string};
export type AnomalyResult={anomalyScore:number;threshold:number;flagged:boolean;modelVersion:string;modelArtifactSha256:string;inputSha256:string;advisoryNote:string;eventCount:number;elapsedDays:number;maxGapHours:number;meanGapHours:number;uniqueActivities:number;repeatedActivityCount:number;resourceHandoffs:number;institutionSwitches:number;distinctResources:number};
export type WorkflowTimeline={eventTimeZone?:string;events:{timestamp:string}[]};
export type RequestReport={id:string;creatorActorId?:string;kind:"early"|"anomaly";state:string;sourceVersion:string;createdAtUtc:string;investigationId:string|null;earlyAssessmentId:string|null;anomalyAssessmentId:string|null;result:AnomalyResult|{result:{overallStatus:string;indicatorResults:Indicator[]}}|null;projection?:WorkflowTimeline|null;audit:{operation:string;createdAtUtc:string}[]};
export const namedScenario="kiwula-kale-named-route-v2";
export async function officerApi<T>(route:string,body?:unknown,signal?:AbortSignal):Promise<T>{
 const session=readDevelopmentSession();if(!session)throw new Error("Sign in as the DS officer.");
 const response=await fetch(developmentApiBase()+"/api/governance-intelligence/"+route,{method:body===undefined?"GET":"POST",cache:"no-store",signal,headers:{Authorization:`Bearer ${session.accessToken}`,...(body===undefined?{}:{"Content-Type":"application/json"})},body:body===undefined?undefined:JSON.stringify(body)});
 if(!response.ok)throw new Error(response.status===403?"This DS account cannot access this record.":response.status===409?"Another request is Pending/Unknown, or the key has different input. Refresh history; do not retry uncertain inference.":response.status===401?"Session expired. Sign in again.":"Request unavailable. Refresh history before submitting again; an earlier request may have been saved.");
 return response.json() as Promise<T>;
}
export function pendingOfficerKey(actor:string,kind:string,storage:Pick<Storage,"getItem"|"setItem">,make=()=>crypto.randomUUID()){
 const name=`c4-officer-pending/1/${actor}/${kind}`;const old=storage.getItem(name);if(old)return old;const key=make();storage.setItem(name,key);return key;
}
export function clearOfficerKey(actor:string,kind:string){sessionStorage.removeItem(`c4-officer-pending/1/${actor}/${kind}`);}
// Saved-report read authority does not grant access to another officer's draft.
export async function loadOwnedReportInvestigation(report:RequestReport|undefined,actor:string|null,read=(id:string)=>officerApi<Investigation>(`officer-investigations/${encodeURIComponent(id)}`)):Promise<Investigation|null>{
 if(!actor||report?.creatorActorId!==actor||!report.investigationId)return null;
 return read(report.investigationId);
}

import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
import type {RegulatoryFinding} from "./independentRegulatoryReports";
export type FollowupInput={submissionKey:string;ruleCode:string;operation:string;reviewOutcome:string|null;missingInformation:string|null;remarks:string;references:string[];expectedRevision:number};
export type FollowupEvent={id:string;evaluationId:string;ruleCode:string;ruleVersion:string;actorId:string;createdAtUtc:string;operation:string;reviewOutcome:string|null;missingInformation:string|null;remarks:string;references:string[];status:string;revision:number;previousEventId:string|null;eventSha256:string};
export type ClosureEligibility={ruleCode:string;ruleVersion:string;canClose:boolean;evidenceRequired:boolean;reason:string;releasesProgressionHold?:boolean};
export type FollowupHistory={canWrite:boolean;history:FollowupEvent[];closureEligibility?:ClosureEligibility[]};
export type FollowupSaved={event:FollowupEvent;currentRevision:number;reused:boolean};
export type PreviousFollowup={evaluationId:string;evaluationTimestamp:string;event:FollowupEvent};
export type PreviousFollowupPage={items:PreviousFollowup[];nextCursor:string|null};
export const humanReviewOutcomes=["NoAdditionalConcern","AdditionalInformationRequired","FurtherInstitutionalLegalReview"] as const;
export function canRecordNoFollowup(f:RegulatoryFinding){return f.status==="Compliant"&&!f.isBlocking&&!f.requiresHumanReview;}
export function actionableFollowupFinding(f:RegulatoryFinding){return f.status==="InsufficientInformation"||f.status==="RequiresHumanReview";}
export function followupOperationLabel(operation:string){return operation==="FindingVerifiedClosed"?"No Issues Found — Verify and Close":operation==="NoFollowupRequired"?"No Follow-up Required":operation==="ClarificationRequested"?"Request Clarification":"Record Human Review";}
export function followupOperations(f:RegulatoryFinding){return [...(canRecordNoFollowup(f)?["NoFollowupRequired"]:[]),...(f.status==="InsufficientInformation"?["ClarificationRequested"]:[]),...(f.requiresHumanReview||["RequiresHumanReview","RequiresReview"].includes(f.status)?["HumanReviewRecorded"]:[]),...(actionableFollowupFinding(f)?["FindingVerifiedClosed"]:[])];}
export function latestFindingFollowup(events:readonly FollowupEvent[],finding:RegulatoryFinding){return events.filter(e=>e.ruleCode===finding.ruleCode&&e.ruleVersion===finding.ruleVersion).reduce<FollowupEvent|undefined>((a,b)=>!a||b.revision>a.revision?b:a,undefined);}
export function findingOfficerStatus(event:FollowupEvent|undefined){return event?.operation==="FindingVerifiedClosed"?"Verified & Closed":event?.operation==="HumanReviewRecorded"&&event.reviewOutcome==="NoAdditionalConcern"?"Human review recorded — no additional concern":"Unresolved";}
export function followupRevision(history:readonly FollowupEvent[],rule:string){return history.filter(x=>x.ruleCode===rule).reduce((n,x)=>Math.max(n,x.revision),0);}
export function followupDraftKey(id:string){const session=readDevelopmentSession();if(!session)throw new Error("Sign in again.");return `c4.regulatory-followup:${session.user.actorId}:${id}`;}
export class FollowupRejectedError extends Error {}
async function request<T>(id:string,signal?:AbortSignal,input?:FollowupInput):Promise<T>{
 const session=readDevelopmentSession();if(!session)throw new Error("Sign in again.");
 const response=await fetch(`${developmentApiBase()}/api/governance-intelligence/regulatory-assessments/${encodeURIComponent(id)}/follow-ups`,{method:input?"POST":"GET",cache:"no-store",signal,headers:{Authorization:`Bearer ${session.accessToken}`,...(input?{"Content-Type":"application/json"}:{})},body:input?JSON.stringify(input):undefined});
 let validationMessage="";if(response.status===400){try{const problem=await response.json();if(typeof problem.message==="string")validationMessage=problem.message.slice(0,600);}catch{}}
 if(!response.ok)throw new (response.status===400||response.status===409?FollowupRejectedError:Error)(response.status===409?"Revision or submission content changed. Read the history before deliberately replaying the same request.":response.status===404?"Follow-up is unavailable to this account or has not been deployed.":response.status===400?(validationMessage||"The submitted follow-up is invalid. Review the rule, required text and references."):"Follow-up persistence or authorization is unavailable. A saved result is not confirmed; no screening was run.");
 return response.json() as Promise<T>;
}
export const regulatoryFollowups={read:(id:string,signal?:AbortSignal)=>request<FollowupHistory>(id,signal),record:(id:string,input:FollowupInput)=>request<FollowupSaved>(id,undefined,input),previous:async(id:string,cursor:string|null,signal?:AbortSignal)=>{
 const session=readDevelopmentSession();if(!session)throw new Error("Sign in again.");
 const query=new URLSearchParams({limit:"25"});if(cursor)query.set("cursor",cursor);
 const response=await fetch(`${developmentApiBase()}/api/governance-intelligence/regulatory-assessments/${encodeURIComponent(id)}/follow-ups/previous?${query}`,{cache:"no-store",signal,headers:{Authorization:`Bearer ${session.accessToken}`}});
 if(!response.ok)throw new Error("Previous follow-ups are unavailable to the current account. Refresh after checking your session.");
 return response.json() as Promise<PreviousFollowupPage>;
}};

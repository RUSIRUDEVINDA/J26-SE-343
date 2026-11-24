import type {HandlingView} from "./complaintHandling";
import {apiGet,apiPost} from "@/lib/apiClient";
import {readDevelopmentSession} from "@/lib/developmentSession";
export type Attempt={id:string;startedAtUtc:string;finishedAtUtc:string|null;state:string;assessmentId:string|null;failureCode:string|null};
export type Complaint={id:string;applicantId:string;subject:string;complaintText:string;caseId:string|null;parcelId:string|null;submittedAtUtc:string;revision:number;classificationState:string;reviewStatus:string;attempts:Attempt[];landAddress?:string|null;unverifiedCaseReference?:string|null;unverifiedParcelReference?:string|null;reviews:{id:string;actorId:string;atUtc:string;status:string;reason:string}[]};
export type Classification={assessmentId:string;predictedCategory:string;modelVersion:string;classProbabilities:Record<string,number>;advisoryNote:string;closedSetNote:string;assessedAtUtc:string};
export type ComplaintDetail={complaint:Complaint;classifications:Classification[];handling?:HandlingView};
export type ComplaintSubmission={submissionKey:string;subject:string;complaintText:string;caseReference:string|null;parcelId:string|null;landAddress?:string|null};
const base="/api/governance-intelligence/registered-complaints";
function session(){if(!readDevelopmentSession())throw new Error("Sign in to manage complaints.");}
export function classificationMessage(c:Complaint){return c.classificationState==="Classified"?"Model classification saved. Allegations remain unverified.":c.classificationState==="InProgress"?"Classification in progress. If interrupted, retry after five minutes.":c.classificationState==="Failed"?"Complaint saved; classification failed. A controlled retry is available.":"Complaint saved; classification pending.";}
export function canRetry(c:Complaint,now=Date.now()){const last=c.attempts.at(-1);return c.classificationState!=="Classified"&&(c.classificationState!=="InProgress"||(!!last&&new Date(last.startedAtUtc).getTime()+300000<=now));}
export function submissionKey(){const key="registered-complaint-submission-key";let value=sessionStorage.getItem(key);if(!value){value=crypto.randomUUID();sessionStorage.setItem(key,value);}return value;}
export function clearSubmissionKey(){sessionStorage.removeItem("registered-complaint-submission-key");}
export const complaintsApi={
 intakeCapabilities:()=>{session();return apiGet<{version:string;unverifiedLandLocation:boolean}>(`${base}/intake-capabilities`,{cache:"no-store"},"none");},
 list:()=>{session();return apiGet<Complaint[]>(base,{cache:"no-store"},"none");},
 detail:(id:string)=>{session();return apiGet<ComplaintDetail>(`${base}/${encodeURIComponent(id)}`,{cache:"no-store"},"none");},
 submit:(data:ComplaintSubmission)=>{session();return apiPost<Complaint>(base,data,undefined,"none");},
 retry:(c:Complaint)=>{session();return apiPost<Complaint>(`${base}/${c.id}/classification-retries`,{revision:c.revision},undefined,"none");},
 review:(c:Complaint,status:string,reason:string,expectedHandlingRevision:number=0)=>{session();return apiPost<Complaint>(`${base}/${c.id}/reviews`,{revision:c.revision,status,reason,expectedHandlingRevision},undefined,"none");}
};

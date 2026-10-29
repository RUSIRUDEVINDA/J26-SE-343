import {ApiClientError} from "@/lib/apiClient";
import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
export type DemoProjection = {caseId:string;inputSha256:string;eventTimeZone:string;events:{institutionCode?:string;eventOutcome?:string;eventSeq:number;activity:string;timestamp:string;anomalyInputTimestamp:string;resource:string}[];provenance:Record<string,unknown>};
export type DemoAssessment = {assessmentId:string;anomalyScore:number;threshold:number;flagged:boolean;modelVersion:string;modelArtifactSha256:string|null;inputSha256:string;advisoryNote:string;eventCount:number;elapsedDays:number;maxGapHours:number;meanGapHours:number;uniqueActivities:number;repeatedActivityCount:number;resourceHandoffs:number;institutionSwitches:number;distinctResources:number};
export type DemoReport = {id:string;creatorActorId:string;scenario:string;version:string;createdAtUtc:string;state:string;failure:string|null;projection:DemoProjection;assessment:DemoAssessment|null;attempt:number;attempts:{number:number;state:string;finishedAtUtc:string;failure:string|null}[]};
export type DemoChoice = {scenario:string;name:string;description:string;version:string;canRun:boolean;projection:DemoProjection};
export function analysisMessage(report:DemoReport){return report.state==="Saved"?"Actual model result saved.":report.state==="Failed"?"Analysis failed. No prediction is inferred; explicit retry is available.":"Submission pending or interrupted. Refresh saved history; do not start another inference.";}
const base="/api/governance-intelligence/post-workflow-demonstrations";
export function postWorkflowError(status:number,body:Record<string,unknown>,report=false){
 const detail=typeof body.detail==="string"?body.detail:typeof body.title==="string"?body.title:"";
 if(status===401)return "Sign in with your officer account. Your session may have expired.";
 if(status===403)return "Access denied. This officer account cannot read or run this demonstration report.";
 if(status===404)return report?"Saved report not found or unavailable to this account.":"The running API does not expose the post-workflow demonstration endpoint (HTTP 404). Deploy the updated API after controlled activation approval.";
 if(status===0)return "The post-workflow API is unreachable. Check the existing API service and connection.";
 return `${detail||"Post-workflow request failed"} (HTTP ${status})${typeof body.traceId==="string"?` Reference: ${body.traceId}`:""}`;
}
// One epoch per mounted page/session. Aborted responses can never restore another account's data.
export function createPostWorkflowScope(){let epoch=0;let abort=new AbortController();return {
 capture:()=>({epoch,signal:abort.signal}),current:(value:number)=>value===epoch&&!abort.signal.aborted,
 invalidate:()=>{abort.abort();abort=new AbortController();epoch++;}
};}
async function request<T>(suffix:string,signal?:AbortSignal,data?:unknown):Promise<T>{
 const session=readDevelopmentSession();if(!session)throw new ApiClientError({status:401,title:"Sign-in required",detail:postWorkflowError(401,{})});
 let response:Response;
 try{response=await fetch(developmentApiBase()+base+suffix,{method:data===undefined?"GET":"POST",cache:"no-store",signal,
  headers:{Authorization:`Bearer ${session.accessToken}`,...(data===undefined?{}:{"Content-Type":"application/json"})},body:data===undefined?undefined:JSON.stringify(data)});}
 catch(e){if(signal?.aborted)throw e;throw new ApiClientError({status:0,title:"API unavailable",detail:postWorkflowError(0,{})});}
 if(!response.ok){let body:Record<string,unknown>={};try{body=await response.json();}catch{/* Empty routing errors are valid. */}
  throw new ApiClientError({status:response.status,title:"Post-workflow request failed",detail:postWorkflowError(response.status,body,/^\/[0-9a-f-]{36}$/i.test(suffix))});}
 return response.json() as Promise<T>;
}
export const postWorkflowApi={
 catalogue:(signal?:AbortSignal)=>request<DemoChoice[]>("/catalogue",signal),
 history:(signal?:AbortSignal)=>request<DemoReport[]>("",signal),
 get:(id:string,signal?:AbortSignal)=>request<DemoReport>(`/${encodeURIComponent(id)}`,signal),
 run:(scenario:string,retry=false,expectedAttempt=0,signal?:AbortSignal)=>request<DemoReport>(retry?"/retries":"",signal,{scenario,expectedAttempt})
};

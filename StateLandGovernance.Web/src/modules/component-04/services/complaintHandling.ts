import {developmentApiBase,readDevelopmentSession} from "@/lib/developmentSession";
import {C4RequestError,confirmedJson} from "./requestOutcome";
import {apiGet} from "@/lib/apiClient";
export type HandlingState={revision:number;status:string;mode:string;responsibleActorId:string|null;assignmentEventId:string|null;source:string};
export type HandlingItem={id:string;revision:number;operation:string;status:string;atUtc:string;publicSummary:string;actorId:string|null;reason:string|null;details:Record<string,string>|null;evidenceReferences:{kind:string;reference:string;label:string}[]|null};
export type HandlingView={state:HandlingState;allowedActions:string[];history:HandlingItem[]};
export type HandlingAction={submissionKey:string;expectedHandlingRevision:number;operation:string;reason:string;details:Record<string,string>;evidenceReferences:{kind:string;reference:string;label:string}[];publicSummary:string};
export const handlingFields=(operation:string)=>operation==="Resolve"?["outcome","findings","actionsTaken","decisionBasis"]:operation==="RecordFindings"?["findings","actionsTaken"]:operation==="Reopen"?["proceduralReason"]:operation==="Close"?[]:["notes"];
export function evidenceReferences(text:string):HandlingAction["evidenceReferences"]{
 if(!text.trim())return [];
 const rows=text.split("\n").filter(x=>x.trim()).map(line=>{const [kind,reference,label,...extra]=line.split("|").map(x=>x.trim());if(extra.length||!["C4Record","ExternalReference"].includes(kind)||!reference||!label)throw new Error("Use Kind | reference | label for each evidence reference.");return {kind,reference,label};});
 if(rows.length>20)throw new Error("At most 20 references are allowed.");return rows;
}
function base(central:boolean){return `/api/governance-intelligence/${central?"central-complaints":"registered-complaints"}`;}
export const complaintHandlingApi={
 view:(id:string,central:boolean)=>apiGet<HandlingView>(`${base(central)}/${encodeURIComponent(id)}/actions`,{cache:"no-store"},"none"),
 act:async(id:string,central:boolean,input:HandlingAction)=>{
 const session=readDevelopmentSession();if(!session)throw new C4RequestError("Sign in again.","rejected",401);
 let response:Response;try{response=await fetch(`${developmentApiBase()}${base(central)}/${encodeURIComponent(id)}/actions`,{signal:AbortSignal.timeout(60000),method:"POST",cache:"no-store",headers:{Authorization:`Bearer ${session.accessToken}`,"Content-Type":"application/json"},body:JSON.stringify(input)});}catch{throw new C4RequestError("The action response was lost. Refresh history before replaying the retained request.","uncertain");}
 return confirmedJson<{eventId:string;reused:boolean;current:HandlingState}>(response,true);
 }
};

export function orderedHandlingHistory(view:HandlingView){
 const rows=[...new Map(view.history.map(row=>[row.id,row])).values()].sort((a,b)=>a.revision-b.revision);
 return rows.map((row,index)=>({...row,fromStatus:index>0&&rows[index-1].revision===row.revision-1?rows[index-1].status:null}));
}

import {officerApi,type Investigation,type RequestReport} from "./officerFlows";
import {investigationEvidence,type InvestigationEvidence} from "./investigationEvidence";
import {manualEvidence,type EvidenceView} from "./manualEvidence";
import {getRegulatoryReport,type RegulatoryReport} from "./independentRegulatoryReports";
import {regulatoryFollowups,type FollowupHistory,type FollowupEvent} from "./regulatoryFollowups";
import {kiwulaApi,type KiwulaReport} from "./kiwulaAssessments";
import {readSequence,type SequenceReadiness} from "./officerSequence";

export type SummarySelection={earlyRequestId:string|null;regulatoryEvaluationId:string|null;conflictReportId:string|null};
export type SavedSection<T>={state:"Missing"|"Pending"|"Saved"|"Unavailable";value:T|null;message:string};
export type CombinedFindings={selection:SummarySelection;proposalId:string;readiness:SequenceReadiness;
 early:SavedSection<RequestReport>;investigation:SavedSection<Investigation>;completion:SavedSection<InvestigationEvidence>;
 evidence:SavedSection<EvidenceView>;regulatory:SavedSection<RegulatoryReport>;followups:SavedSection<FollowupHistory>;
 conflict:SavedSection<KiwulaReport>;conflictOptions:KiwulaReport[];conflictOptionsUnavailable:boolean};
export const summaryKey=(s:SummarySelection)=>JSON.stringify([s.earlyRequestId,s.regulatoryEvaluationId,s.conflictReportId]);
export function latestApplicableFollowup(history:readonly FollowupEvent[],ruleCode:string,ruleVersion:string){
 return history.filter(x=>x.ruleCode===ruleCode&&x.ruleVersion===ruleVersion).reduce<FollowupEvent|null>((last,event)=>!last||event.revision>last.revision?event:last,null);
}
export const combinedReaders={sequence:readSequence,early:(id:string)=>officerApi<RequestReport>(`officer-assessments/${encodeURIComponent(id)}`),
 investigation:(id:string)=>officerApi<Investigation>(`officer-investigations/${encodeURIComponent(id)}`),completion:investigationEvidence.read,
 evidence:manualEvidence.read,regulatory:getRegulatoryReport,followups:regulatoryFollowups.read,conflict:kiwulaApi.get,conflictHistory:kiwulaApi.history};
type Readers=typeof combinedReaders;
const missing=<T>(message:string):SavedSection<T>=>({state:"Missing",value:null,message});
const unavailable=<T>(message:string):SavedSection<T>=>({state:"Unavailable",value:null,message});
const saved=<T>(value:T):SavedSection<T>=>({state:"Saved",value,message:"Retrieved from saved storage"});
async function optional<T>(read:()=>Promise<T>,message:string){try{return saved(await read());}catch{return unavailable<T>(message);}}

/** GET-only composition. Existing backend policies remain authoritative, including a final live eligibility recheck. */
export async function loadCombinedFindings(selection:SummarySelection,proposalId:string,actor:string,readers:Readers=combinedReaders):Promise<CombinedFindings>{
 const {earlyRequestId,regulatoryEvaluationId,conflictReportId}=selection;
 const readiness=await readers.sequence(earlyRequestId,regulatoryEvaluationId);
 const view:CombinedFindings={selection:{...selection},proposalId,readiness,early:missing("No Early request selected."),investigation:missing("No saved investigation selected."),completion:missing("Finalization has not been confirmed."),evidence:missing("No saved Early assessment selected."),regulatory:missing("No Regulatory evaluation selected."),followups:missing("No Regulatory evaluation selected."),conflict:missing("Not available / Not assessed. No saved Conflict report explicitly selected."),conflictOptions:[],conflictOptionsUnavailable:false};
 if(!earlyRequestId){if(regulatoryEvaluationId)view.regulatory=unavailable("The selected Regulatory evaluation cannot be associated without its exact Early request.");return view;}
 const early=await readers.early(earlyRequestId);
 if(early.id!==earlyRequestId||early.kind!=="early")throw new Error("Selected Early identity is inconsistent; no summary displayed.");
 if(!early.investigationId)throw new Error("The selected Early request has no investigation/proposal provenance.");
 view.early=early.state==="Saved"&&early.result&&"result" in early.result?saved(early):{state:"Pending",value:early,message:`Request state: ${early.state}. No confirmed saved Early outcome.`};
 if(early.investigationId){
  const investigation=await readers.investigation(early.investigationId);
  if(investigation.id!==early.investigationId||investigation.proposalId!==proposalId||investigation.sourceVersion!==early.sourceVersion)throw new Error("The selected investigation/proposal provenance does not match.");
  view.investigation=saved(investigation);
  const [completion,evidence]=await Promise.all([optional(()=>readers.completion(investigation.id),"Manual finalization and pre-screening evidence are unavailable to this session."),early.state==="Saved"?optional(()=>readers.evidence(early.id),"Saved assessment evidence/history is unavailable to this session."):Promise.resolve(missing<EvidenceView>("Evidence review is available only after a saved screening."))]);
  if(completion.value&&(completion.value.attachments.some(x=>x.investigationId!==investigation.id)||completion.value.finalization&&completion.value.finalization.investigationId!==investigation.id))throw new Error("Investigation evidence identity mismatch.");
  if(evidence.value&&(evidence.value.attachments.some(x=>x.requestId!==early.id)||evidence.value.history.some(x=>x.requestId!==early.id)))throw new Error("Saved evidence identity mismatch.");
  view.completion=completion;view.evidence=evidence;
 }
 if(regulatoryEvaluationId){
  if(readiness.regulatoryEvaluationId!==regulatoryEvaluationId||readiness.earlyRequestId!==earlyRequestId){view.regulatory=unavailable("The backend has not confirmed this exact Early/Regulatory chain.");}
  else{
   const regulatory=await readers.regulatory(regulatoryEvaluationId);
   if(regulatory.metadata.id!==regulatoryEvaluationId||regulatory.metadata.proposalId!==proposalId||regulatory.provenance?.earlyRequestId!==earlyRequestId||regulatory.provenance.creatorActorId!==actor)throw new Error("The selected Regulatory provenance does not match the owned Early/proposal chain.");
   view.regulatory=saved(regulatory);
   view.followups=await optional(()=>readers.followups(regulatoryEvaluationId),"Follow-up history is unavailable; no decision or resolution is inferred.");
   if(view.followups.value?.history.some(x=>x.evaluationId!==regulatoryEvaluationId))throw new Error("Regulatory follow-up identity mismatch.");
  }
 }
 if(conflictReportId)view.conflict=unavailable("No persisted binding establishes a Conflict result for this active Early/Regulatory chain. Historical decision comparisons remain in Assessment History and do not establish current clearance.");
 // Do not retain a summary after live DS eligibility or the selected chain has been revoked/changed.
 view.readiness=await readers.sequence(earlyRequestId,regulatoryEvaluationId);
 return view;
}

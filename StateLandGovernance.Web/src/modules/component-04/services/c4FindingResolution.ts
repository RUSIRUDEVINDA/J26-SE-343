import type {RegulatoryFinding} from "./independentRegulatoryReports";
import type {FollowupEvent} from "./regulatoryFollowups";
// Saved API history has already passed ownership, canonical audit and revision checks.
export function isAddressingFollowup(f:RegulatoryFinding,e:FollowupEvent){
 const text=(s:string|null|undefined)=>s?.trim()??"";
 if(e.status!=="Recorded"||!Number.isInteger(e.revision)||e.revision<1||!e.id||!Array.isArray(e.references)||e.references.length>10||e.references.some(r=>text(r).length<1||text(r).length>500)||text(e.remarks).length<20||text(e.remarks).length>2000)return false;
 if(e.operation==="ClarificationRequested")return f.status==="InsufficientInformation"&&e.reviewOutcome===null&&text(e.missingInformation).length>=20&&text(e.missingInformation).length<=2000;
 return e.operation==="HumanReviewRecorded"&&(f.requiresHumanReview||["RequiresHumanReview","RequiresReview"].includes(f.status))&&e.missingInformation===null&&["NoAdditionalConcern","AdditionalInformationRequired","FurtherInstitutionalLegalReview"].includes(e.reviewOutcome??"");
}
export function c4FindingResolution(f:RegulatoryFinding,events:readonly FollowupEvent[],evaluationId:string){
 if(f.status==="Compliant"||(f.status==="NotApplicable"&&f.applicability==="NotApplicable"&&!f.isBlocking&&!f.requiresHumanReview))return {resolved:true,label:"Addressed",event:undefined,concern:undefined};
 const event=events.filter(e=>e.evaluationId===evaluationId&&e.status==="Recorded"&&e.ruleCode===f.ruleCode&&e.ruleVersion===f.ruleVersion&&(e.operation==="FindingVerifiedClosed"||isAddressingFollowup(f,e))).reduce<FollowupEvent|undefined>((a,b)=>!a||b.revision>a.revision?b:a,undefined);
 if(["InsufficientInformation","RequiresHumanReview"].includes(f.status)&&event?.operation==="FindingVerifiedClosed"&&!event.reviewOutcome&&!event.missingInformation)return {resolved:true,label:"Verified & Closed",event};
 if(event&&isAddressingFollowup(f,event))return {resolved:true,label:"Addressed",event,concern:event.operation==="ClarificationRequested"?"Clarification Requested / Outstanding":event.reviewOutcome==="NoAdditionalConcern"?"No additional concern recorded":event.reviewOutcome==="AdditionalInformationRequired"?"Additional information required / Outstanding":"Further institutional/legal review required / Outstanding"};
 return {resolved:false,label:"Awaiting Officer Follow-up",event,concern:undefined};
}

import type {CaseDetailResponse,CaseCompletenessSummaryResponse} from "../types/caseManagement";
export function caseNextAction(detail:CaseDetailResponse,summary:CaseCompletenessSummaryResponse|null,hasSnapshot:boolean) {
 const action=(view:"documents"|"facts"|"completeness"|"screening"|"execution"|"fulfillment",stage:string,text:string)=>({view,stage,text});
 if(["ApprovedWithConditions","ReadyForHandoff","HandedOff"].includes(detail.status)) return action("fulfillment","Fulfillment & Handoff",detail.status==="HandedOff"?"Inspect the persisted handoff and acknowledgement.":"Resolve persisted fulfillment conditions and handoff prerequisites.");
 if(detail.status==="InWorkflow")return action("execution","Department Approvals","Inspect the approved plan, institutional requests and any blocked decisions; current inputs are rechecked before action.");
 if(!detail.proposalSelection?.documentId)return action("documents","Submission","Upload or select the Proposal; retain other document classifications.");
 if(!hasSnapshot)return action("facts","Verification & Completeness",detail.canVerifyFacts?"Review source-backed facts and publish the selected Proposal snapshot.":"Ask the case administrator for exact case/document reviewer and publisher assignments.");
 if(!summary)return action("completeness","Verification & Completeness","Load completeness readiness; unavailable checks cannot establish completion.");
 if(!summary.documentRequirementsConfigured||!summary.proposalTemplateConfigured)return action("completeness","Verification & Completeness","Requirements not configured: obtain the applicable versioned project configuration.");
 if(!summary.isDocumentCompletenessCurrent||summary.documentCompleteness?.outcome!=="Complete")return action("completeness","Verification & Completeness","Review supporting-document classification and completeness findings.");
 if(!summary.isProposalContentCurrent||summary.proposalContent?.outcome!=="Complete"||!summary.proposalContent.isConfirmed)return action("completeness","Verification & Completeness",`Resolve proposal-content findings (${(summary.proposalContent?.missingMandatory.length||0)+(summary.proposalContent?.reviewRequiredMandatory.length||0)} outstanding mandatory items); explicitly review and confirm the assessment.`);
 return action("screening","C4 Screening","Check current C4 prerequisites and findings before planning department approvals.");
}

import {test} from "node:test";
import assert from "node:assert/strict";
import {caseNextAction} from "../../src/modules/workflow-governance/services/caseNextAction";
import type {CaseDetailResponse,CaseCompletenessSummaryResponse} from "../../src/modules/workflow-governance/types/caseManagement";
const c={status:"Draft",canVerifyFacts:true,proposalSelection:{documentId:"proposal"}} as CaseDetailResponse;
const summary={documentRequirementsConfigured:true,proposalTemplateConfigured:true,isDocumentCompletenessCurrent:true,isProposalContentCurrent:true,documentCompleteness:{outcome:"Complete"},proposalContent:{outcome:"HumanReviewRequired",isConfirmed:true,missingMandatory:[],reviewRequiredMandatory:[{}]}} as unknown as CaseCompletenessSummaryResponse;
test("A confirmed but incomplete proposal still directs the officer to content review",()=>{
 const next=caseNextAction(c,summary,true);assert.equal(next.view,"completeness");assert.match(next.text,/1 outstanding/);assert.doesNotMatch(next.text,/classification/);
});
test("Missing and unconfigured assessments never direct the user to screening",()=>{
 assert.equal(caseNextAction(c,null,true).view,"completeness");
 assert.match(caseNextAction(c,{...summary,proposalTemplateConfigured:false},true).text,/not configured/);
});
test("Published snapshot alone does not imply completed verification and completeness stage",()=>{assert.equal(caseNextAction(c,summary,true).stage,"Verification & Completeness");});
test("Persisted workflow and handoff states lead to their actual readiness screens",()=>{
 assert.equal(caseNextAction({...c,status:"InWorkflow"},summary,true).view,"execution");
 assert.equal(caseNextAction({...c,status:"HandedOff"},summary,true).view,"fulfillment");
});

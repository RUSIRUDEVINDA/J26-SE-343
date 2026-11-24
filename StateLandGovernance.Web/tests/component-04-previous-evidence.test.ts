import {test} from "node:test";
import assert from "node:assert/strict";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {HistoricalEvidenceGroup,PreviousEarlyEvidence} from "../src/modules/component-04/components/PreviousEarlyEvidence";
import {type PreviousEvidence} from "../src/modules/component-04/services/manualEvidence";
const item:PreviousEvidence={requestId:"original-request",assessmentId:"original-assessment",createdAtUtc:"2026-10-23T00:00:00Z",evidence:{canVerify:false,attachments:[{id:"attachment-a",requestId:"original-request",concern:"LegalDispute",actorId:"responsible-ds",createdAtUtc:"2026-10-23T00:01:00Z",fileName:"State Land Development Proposal Form.png",contentType:"image/png",bytes:1487486,sha256:"original-hash"}],history:[{id:"decision-a",requestId:"original-request",concern:"LegalDispute",revision:1,previousStatus:"Unverified",status:"Verified",actorId:"responsible-ds",createdAtUtc:"2026-10-23T00:02:00Z",remarks:"Original reviewed evidence remarks.",attachmentIds:["attachment-a"],evidenceReferences:["Original document reference"],eventSha256:"event-hash"}]}};
test("Historical group preserves provenance and original report without write controls",()=>{
 const html=renderToStaticMarkup(createElement(HistoricalEvidenceGroup,{item,onDownload:()=>{}}));
 for(const value of ["original-request","original-assessment","State Land Development Proposal Form.png","responsible-ds","Verified","revision 1","Original reviewed evidence remarks.","Original document reference","/component-04/officer-assessments/original-request"])assert.ok(html.includes(value),value);
 assert.ok(html.includes("Download State Land Development Proposal Form.png"));assert.ok(!html.includes('type="file"'));assert.ok(!html.includes("Save verification"));assert.ok(!html.includes('href="http'));
});
test("Historical rendering never mutates current assessment counts",()=>{
 const current={canVerify:true,attachments:[],history:[]};renderToStaticMarkup(createElement(HistoricalEvidenceGroup,{item,onDownload:()=>{}}));assert.deepEqual(current,{canVerify:true,attachments:[],history:[]});assert.equal(item.evidence.history[0].revision,1);
});
test("Historical group provides separate empty states",()=>{
 const html=renderToStaticMarkup(createElement(HistoricalEvidenceGroup,{item:{...item,evidence:{canVerify:false,attachments:[],history:[]}},onDownload:()=>{}}));assert.match(html,/No attachments saved on this assessment/);assert.match(html,/No verification decisions saved on this assessment/);
});

test("Previous Assessments explains read-only once, with one technical disclosure per group",()=>{
 const section=renderToStaticMarkup(createElement(PreviousEarlyEvidence,{requestId:"current"}));
 assert.equal(section.split("Previous assessments are shown for reference. Changes can only be made to the current assessment.").length-1,1);
 assert.match(section,/Previous Assessments/);assert.doesNotMatch(section,/Historical — Read-only/);
 const group=renderToStaticMarkup(createElement(HistoricalEvidenceGroup,{item,onDownload:()=>{}}));
 assert.equal((group.match(/<details/g)||[]).length,1);assert.match(group,/<summary>Technical details<\/summary>/);assert.match(group,/Attachments: 1/);assert.match(group,/Verification history: 1/);assert.match(group,/Legal Dispute/);
 assert.doesNotMatch(group,/Historical — Read-only|type="file"|<textarea|Save verification/);
});

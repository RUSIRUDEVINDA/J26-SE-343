import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {groupPreviousFollowups,PreviousRegulatoryFollowups} from "../src/modules/component-04/components/PreviousRegulatoryFollowups";
import type {PreviousFollowup} from "../src/modules/component-04/services/regulatoryFollowups";
import {handlingFields} from "../src/modules/component-04/services/complaintHandling";
import {ComplaintHandlingFields} from "../src/modules/component-04/components/ComplaintHandlingPanel";

test("Historical pages group and deduplicate original events without changing revisions",()=>{
 const a={evaluationId:"A",evaluationTimestamp:"2026-10-23T00:00:00Z",event:{id:"event-A",revision:3}} as PreviousFollowup;
 const b={...a,event:{...a.event,id:"event-B",revision:4}};
 const result=groupPreviousFollowups([a,b,a]);assert.equal(result.length,1);assert.deepEqual(result[0].events.map(x=>x.revision),[3,4]);assert.equal(result[0].id,"A");
});
test("Every handling action renders vertically labeled controls, helpers and unchanged validation",()=>{
 for(const operation of ["TakeDirect","StartInvestigation","RecordNote","RequestInformation","RecordFindings","Resolve","Close","Reopen"]){
  const html=renderToStaticMarkup(createElement(ComplaintHandlingFields,{operation,reason:"",details:{},refs:"",summary:"",confirmed:false,busy:false,retained:false,summaryHelp:"summary-help",referenceHelp:"refs-help",setReason:()=>{},setDetails:()=>{},setSummary:()=>{},setRefs:()=>{},setConfirmed:()=>{}}));
  assert.match(html,/Reason<textarea[^>]*required/);assert.match(html,/Applicant-safe summary<textarea[^>]*aria-describedby="summary-help"/);assert.match(html,/Only this summary is shown/);assert.match(html,/Evidence references/);
  for(const field of handlingFields(operation))assert.ok(html.toLowerCase().includes((field==="outcome"?"Choose recorded outcome":field==="decisionBasis"?"Decision Basis":field==="actionsTaken"?"Actions Taken":field==="proceduralReason"?"Procedural Reason":field[0].toUpperCase()+field.slice(1)).toLowerCase()));
  const disabled=renderToStaticMarkup(createElement(ComplaintHandlingFields,{operation,reason:"",details:{},refs:"",summary:"",confirmed:false,busy:true,retained:false,summaryHelp:"summary-help",referenceHelp:"refs-help",setReason:()=>{},setDetails:()=>{},setSummary:()=>{},setRefs:()=>{},setConfirmed:()=>{}}));assert.match(disabled,/disabled/);
 }
});
test("Previous workspace is explicitly read-only and separate from current revisions",()=>{
 const html=renderToStaticMarkup(createElement(PreviousRegulatoryFollowups,{evaluationId:"B"}));assert.match(html,/Previous Regulatory Follow-ups/);assert.equal(html.split("Previous assessments are shown for reference. Changes can only be made to the current assessment.").length-1,1);assert.doesNotMatch(html,/Historical — Read-only/);assert.doesNotMatch(html,/<form|<textarea/);
 const source=readFileSync("src/modules/component-04/components/PreviousRegulatoryFollowups.tsx","utf8");assert.match(source,/development-session-changed/);assert.match(source,/AbortController/);assert.match(source,/state.actorId===actorId/);assert.match(source,/Load more previous follow-ups/);assert.doesNotMatch(source,/\.record\(|method:"POST"/);
});
test("All handling states use the same stacked full-width field styles and preserve payloads",()=>{
 const source=readFileSync("src/modules/component-04/components/ComplaintHandlingPanel.tsx","utf8");assert.match(source,/<form className=\{formStyles.form\}/);assert.match(source,/<label className=\{formStyles.field\} key=\{field\}/);assert.match(source,/className=\{formStyles.confirmation\}/);assert.match(source,/aria-describedby=\{summaryHelp\}/);assert.match(source,/publicSummary:summary/);assert.match(source,/details,evidenceReferences/);
 for(const operation of ["TakeDirect","StartInvestigation","RecordNote","RequestInformation","RecordFindings","Resolve","Close","Reopen"])assert.ok(Array.isArray(handlingFields(operation)));
 const css=readFileSync("src/modules/component-04/components/ComplaintActionForm.module.css","utf8");for(const rule of ["grid-template-columns:minmax(0,1fr)","width:100%","min-width:0","box-sizing:border-box","min-height:100px","min-height:112px",":focus-visible","@media(max-width:600px)"])assert.ok(css.includes(rule));
 const triage=readFileSync("src/modules/component-04/components/RegisteredComplaints.tsx","utf8");assert.match(triage,/Triage status/);assert.match(triage,/Triage Reason/);assert.match(triage,/complaintsApi.review\(detail.complaint,status,reason,detail.handling\?\.state.revision\?\?0\)/);
});

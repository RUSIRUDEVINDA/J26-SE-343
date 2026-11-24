import {test} from "node:test";
import {AppRouterContext,type AppRouterInstance} from "next/dist/shared/lib/app-router-context.shared-runtime.js";
const testRouter={bfcacheId:"test",back(){},forward(){},push(){},replace(){},refresh(){},prefetch(){}} satisfies AppRouterInstance;
import assert from "node:assert/strict";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {MeasuredWorkflowFeatures} from "../src/modules/component-04/components/MeasuredWorkflowFeatures";
import {OfficerAssessmentResult} from "../src/modules/component-04/components/OfficerAssessmentResult";
import type {RequestReport} from "../src/modules/component-04/services/officerFlows";
import {assessWorkflowDuration} from "../src/modules/component-04/services/workflowDuration";
import {KiwulaAssessmentWorkspace} from "../src/modules/component-04/components/KiwulaAssessmentWorkspace";
import {c4Navigation} from "../src/modules/component-04/services/c4Navigation";
test("C4 proposal and regulatory navigation have distinct preserved deep links",()=>{
 assert.equal(c4Navigation.find(x=>x.label==="Proposals")?.href,"/component-04/assessments");
 assert.equal(c4Navigation.find(x=>x.label==="Regulatory Screening")?.href,"/component-04/assessments/regulatory");
 assert.equal(new Set(c4Navigation.map(x=>x.href)).size,c4Navigation.length);
});
test("Proposals initially render only the listing and separate view/screen actions",()=>{
 const html=renderToStaticMarkup(createElement(AppRouterContext.Provider,{value:testRouter},createElement(KiwulaAssessmentWorkspace)));
 assert.match(html,/View Proposal Data/);assert.match(html,/Start Screening/);assert.match(html,/aria-expanded="false"/);
 assert.doesNotMatch(html,/id="proposal-details"|<h2>Proposal information/);
});
test("Regulatory direct route is held until persisted Early prerequisite is selected",()=>{
 const html=renderToStaticMarkup(createElement(AppRouterContext.Provider,{value:testRouter},createElement(KiwulaAssessmentWorkspace,{engine:"regulatory"})));
 assert.match(html,/Complete or explicitly select a finalized saved Early screening/);assert.doesNotMatch(html,/Run Regulatory Screening|View Proposal Data/);
});
test("All nine measured feature values preserve precision and show units separately from model identity",()=>{
 const result={eventCount:7,elapsedDays:17.123456789,maxGapHours:43.25,meanGapHours:13.75,uniqueActivities:6,repeatedActivityCount:1,resourceHandoffs:5,institutionSwitches:4,distinctResources:3,modelVersion:"v1",anomalyScore:.6704623651406526,threshold:.5743688923115832,inputSha256:"input-digest",modelArtifactSha256:"artifact-digest"};
 const html=renderToStaticMarkup(createElement(MeasuredWorkflowFeatures,{result}));
 assert.match(html,/17.123456789/);assert.match(html,/43.25/);assert.match(html,/days/);assert.match(html,/hours/);assert.match(html,/<details[^>]*><summary>Technical details<\/summary>/);assert.doesNotMatch(html.split("<details")[0],/0\.6704623651406526|artifact-digest/);assert.match(html,/0.6704623651406526/);assert.match(html,/artifact-digest/);assert.match(html,/Distinct resources/);
 assert.match(html,/first recorded step to the last recorded step/);assert.match(html,/responsibility moved to a different recorded person or resource/);assert.match(html,/nine measurements below are considered together/i);
 assert.doesNotMatch(html,/Send results to Workflow Governance/);
});
test("Saved workflow result keeps only the disabled governance handoff in its action row",()=>{
 const report={id:"saved-workflow",kind:"anomaly",state:"Saved",sourceVersion:"1",createdAtUtc:"2026-10-25T00:00:00Z",investigationId:null,earlyAssessmentId:null,anomalyAssessmentId:"assessment",audit:[],result:{eventCount:7,elapsedDays:17,maxGapHours:72,meanGapHours:68,uniqueActivities:7,repeatedActivityCount:0,resourceHandoffs:6,institutionSwitches:6,distinctResources:5,anomalyScore:.67,threshold:.57,flagged:true,modelVersion:"v1",modelArtifactSha256:"artifact",inputSha256:"input",advisoryNote:"Advisory."}} satisfies RequestReport;
 const html=renderToStaticMarkup(createElement(OfficerAssessmentResult,{report}));
 assert.match(html,/<div class="toolbar"><button[^>]*disabled=""[^>]*>Send results to Workflow Governance<\/button><\/div>/);
 assert.doesNotMatch(html,/Open Saved Report/);
 assert.match(html,/model&#x27;s reference workflows/);
});
test("Workflow duration display warns only outside the one-week and two-calendar-month boundaries",()=>{
 const timeline=(first:string,last:string)=>({eventTimeZone:"Asia/Colombo",events:[{timestamp:first},{timestamp:last}]});
 assert.deepEqual(assessWorkflowDuration(timeline("2026-02-01T09:00:00+05:30","2026-02-07T08:00:00+05:30"),6.96),{status:"warning-short",message:"Warning: the recorded workflow completed in less than one week."});
 assert.deepEqual(assessWorkflowDuration(timeline("2026-01-31T09:00:00+05:30","2026-03-31T09:00:00+05:30"),59),{status:"acceptable",message:"The recorded workflow duration is acceptable."});
 assert.deepEqual(assessWorkflowDuration(timeline("2026-01-31T09:00:00+05:30","2026-04-01T09:00:00+05:30"),60),{status:"warning-long",message:"Warning: the recorded workflow took more than two calendar months."});
 const html=renderToStaticMarkup(createElement(MeasuredWorkflowFeatures,{result:{eventCount:2,elapsedDays:60,maxGapHours:1440,meanGapHours:1440,uniqueActivities:2,repeatedActivityCount:0,resourceHandoffs:1,institutionSwitches:1,distinctResources:2,modelVersion:"v1",anomalyScore:.67,threshold:.57,inputSha256:"input"},timeline:timeline("2026-01-31T09:00:00+05:30","2026-04-01T09:00:00+05:30")}));
 assert.match(html,/Warning: the recorded workflow took more than two calendar months\./);
 assert.match(html,/<section class="durationCard"[^>]*><h4>Workflow duration<\/h4>/);
 assert.match(html,/Timing guide: a recorded workflow is acceptable when it takes at least one week and no more than two calendar months\./);
});

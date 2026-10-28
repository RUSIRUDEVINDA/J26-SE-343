import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {MeasuredWorkflowFeatures} from "../src/modules/component-04/components/MeasuredWorkflowFeatures";
import {PostWorkflowDemonstrations} from "../src/modules/component-04/components/PostWorkflowDemonstrations";
import {c4NavigationFor} from "../src/modules/component-04/services/c4Navigation";
const metrics={eventCount:7,elapsedDays:17,maxGapHours:72,meanGapHours:68,uniqueActivities:7,repeatedActivityCount:0,resourceHandoffs:6,institutionSwitches:6,distinctResources:5,anomalyScore:.6704623651406526,threshold:.5743688923115832,modelVersion:"saved-model-version",inputSha256:"saved-input-hash",modelArtifactSha256:"saved-artifact-hash"};
for(const flagged of [true,false,undefined])test(`saved flag ${flagged} determines verdict without interpreting its score`,()=>{
 const html=renderToStaticMarkup(createElement(MeasuredWorkflowFeatures,{result:{...metrics,flagged}}));
 const main=html.split("<details")[0];
 assert.match(main,flagged===true?/UNUSUAL WORKFLOW ACTIVITY FLAGGED/:flagged===false?/No unusual workflow activity flagged/:/Flag status unavailable/);
 if(flagged!==true)assert.doesNotMatch(main,/UNUSUAL WORKFLOW ACTIVITY FLAGGED|data-flagged="true"/);
 assert.doesNotMatch(main,/0\.6704623651406526|0\.5743688923115832|saved-model-version|saved-artifact-hash/);
 assert.match(html,/<details[^>]*><summary>Technical details<\/summary>/);assert.doesNotMatch(html,/<details[^>]*\sopen(?:=|>)/);
 for(const value of ["0.6704623651406526","0.5743688923115832","saved-model-version","saved-input-hash","saved-artifact-hash"])assert.ok(html.includes(value));
});
test("DS and Commissioner navbar retain identical independent Anomaly destination",()=>{for(const commissioner of [true,false])assert.equal(c4NavigationFor(commissioner).find(x=>x.label==="Post-Workflow Anomaly")?.href,"/component-04/post-workflow");});
test("independent page renders illustrative preview and deliberate execution without journey completion",()=>{
 const html=renderToStaticMarkup(createElement(PostWorkflowDemonstrations));
 for(const text of ["Post-Workflow Anomaly","Institutional timeline","Run Anomaly Analysis","DS Final Notification","Previous workflow analyses / saved reports"])assert.ok(html.includes(text));
 assert.doesNotMatch(html,/Complete Early|Complete Regulatory|Combined Findings Summary|earlyRequestId|regulatoryEvaluationId/);
 const route=readFileSync("src/app/component-04/[...view]/page.tsx","utf8");assert.match(route,/view\[0\]==="post-workflow"&&view.length===1\)return <PostWorkflowDemonstrations\/>/);
});
test("independent preview preserves source, chronology and explicit saved reports",()=>{
 const preview=readFileSync("src/modules/component-04/components/NamedWorkflowPreview.tsx","utf8");assert.match(preview,/Illustrative workflow preview/);assert.match(preview,/No C3 workflow/);
 const panel=readFileSync("src/modules/component-04/components/OfficerAnomalyPanel.tsx","utf8");assert.match(panel,/officer-assessments\/catalogue/);assert.match(panel,/r.kind==="anomaly"/);assert.match(panel,/state==="Pending"\|\|r.state==="Unknown"/);assert.doesNotMatch(panel,/Refresh Saved Analyses|readSequence|earlyRequestId|regulatoryEvaluationId|canProceedToConflict/);
 const workspace=readFileSync("src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx","utf8");assert.doesNotMatch(workspace,/\["anomaly",|PostWorkflowDemonstrations|OfficerAnomalyPanel/);
});

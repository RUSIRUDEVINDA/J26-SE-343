import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {applicantComplaintReturn} from "../src/modules/component-04/services/applicantComplaintReturn";
import {RegulatoryFindingExplanation} from "../src/modules/component-04/components/RegulatoryFindingExplanation";
import {MeasuredWorkflowFeatures} from "../src/modules/component-04/components/MeasuredWorkflowFeatures";

test("Only the exact applicant complaint return is allowed, only for an applicant session",()=>{
 const user={home:"/workflow-governance/applicant/cases"};
 assert.equal(applicantComplaintReturn(user,"/workflow-governance/applicant/complaints"),"/workflow-governance/applicant/complaints");
 for(const path of [null,"//evil.test","https://evil.test","/component-04","/\\evil.test","/workflow-governance/applicant/complaints?token=x","%2Fworkflow-governance%2Fapplicant%2Fcomplaints","/workflow-governance/applicant/complaints#x"])
  assert.equal(applicantComplaintReturn(user,path),null);
 assert.equal(applicantComplaintReturn({home:"/component-04"},"/workflow-governance/applicant/complaints"),null);
});
test("Complaint sign-in has a visible label and explicit allowlisted return without secrets",()=>{
 const text=readFileSync("src/modules/component-04/components/RegisteredComplaints.tsx","utf8");
 assert.match(text,/href="\/sign-in\?returnTo=%2Fworkflow-governance%2Fapplicant%2Fcomplaints">Sign in<\/Link>/);
 const css=readFileSync("src/modules/component-04/components/RegisteredComplaints.module.css","utf8");
 assert.match(css,/\.primary\{[^}]*color:#fff!important/);
});
test("Regulatory explanations use returned findings without inventing requirements",()=>{
 const f={ruleCode:"NPD-RAT-001",ruleVersion:"1",category:"ProjectRationale",applicability:"Applicable",status:"RequiresHumanReview",severity:"Review",isBlocking:false,requiresHumanReview:true,observedValueSummary:"Rationale needs expert assessment.",expectedRequirement:"State the identified problem.",evidenceStatus:"Unverified",calculationStatus:"NotApplicable",sourceAuthority:"Recorded authority",sourceDocument:"Recorded source",sourceSection:"1",recommendedAction:"Review the rationale."};
 const html=renderToStaticMarkup(createElement(RegulatoryFindingExplanation,{finding:f}));
 for(const value of [f.ruleCode,f.observedValueSummary,f.expectedRequirement,f.sourceDocument,f.recommendedAction,"Why human judgment is needed","not a final legal or lease approval"])assert.ok(html.includes(value));
 const missing=renderToStaticMarkup(createElement(RegulatoryFindingExplanation,{finding:{...f,status:"InsufficientInformation",observedValueSummary:"Applicability is undetermined."}}));
 assert.match(missing,/Applicability is undetermined/);assert.match(missing,/not an inferred document checklist/);
 const compliant=renderToStaticMarkup(createElement(RegulatoryFindingExplanation,{finding:{...f,status:"Compliant",requiresHumanReview:false}}));assert.match(compliant,/Compliant/);
});
test("Anomaly verdict is a heading; nine features and exact technical values remain",()=>{
 const metrics={eventCount:7,elapsedDays:17,maxGapHours:72,meanGapHours:68,uniqueActivities:7,repeatedActivityCount:0,resourceHandoffs:6,institutionSwitches:6,distinctResources:5,anomalyScore:.6704623651406526,threshold:.5743688923115832,modelVersion:"frozen",inputSha256:"input"};
 for(const flagged of [true,false]){
  const html=renderToStaticMarkup(createElement(MeasuredWorkflowFeatures,{result:{...metrics,flagged}}));
  assert.match(html,flagged?/<h3[^>]*>UNUSUAL WORKFLOW ACTIVITY FLAGGED<\/h3>/:/<h3[^>]*>No unusual workflow activity flagged<\/h3>/);
  if(!flagged)assert.doesNotMatch(html,/UNUSUAL WORKFLOW ACTIVITY FLAGGED|data-flagged="true"/);
  const main=html.split("<details")[0];assert.doesNotMatch(main,/0\.6704623651406526|0\.5743688923115832|<dt>Anomaly score<|<dt>Threshold</);
  assert.match(html,/<details[^>]*><summary>Technical details<\/summary>/);assert.doesNotMatch(html,/<details[^>]*\sopen(?:=|>)/);
  assert.match(html,/0.6704623651406526/);assert.match(html,/0.5743688923115832/);
  for(const label of ["Events","Elapsed time","Longest gap","Mean gap","Unique activities","Repeated activities","Resource handoffs","Institution switches","Distinct resources"])assert.ok(html.includes(label));
  assert.match(html,flagged?/not proof of misconduct/:/does not establish compliance or approval/);
 }
 const css=readFileSync("src/modules/component-04/components/MeasuredWorkflowFeatures.module.css","utf8");assert.match(css,/\.flag\{[^}]*font-size:clamp/);assert.match(css,/\.scorePanel\[data-flagged=true\]/);
});
test("Manual evidence labels are UTF-8 without confirmed mojibake",()=>{
 const text=readFileSync("src/modules/component-04/components/ManualEvidenceVerification.tsx","utf8");
 assert.doesNotMatch(text,/â|Â|�/);assert.match(text,/20–2000/);assert.match(text,/· revision/);
});

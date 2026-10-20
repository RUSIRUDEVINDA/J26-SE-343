import {test} from "node:test";
import assert from "node:assert/strict";
import {buildDevelopmentInput,toAnomalyRequest,scenarioOptions} from "../src/modules/component-04/services/developmentInputAdapter";
import {localInputPanelAllowed,matchesCurrentAssessment,requireMatchingEngineCase,type ConsolidatedHistoryItem} from "../src/modules/component-04/services/independentGovernanceApi";
test("All independent scenarios contain input contracts only, never seeded scores or fake extracted provenance",()=>{
 for(const option of scenarioOptions){const f=buildDevelopmentInput("assigned-synthetic-case",option.id);if(f.screeningInput){assert.equal(f.screeningInput.caseId,"assigned-synthetic-case");assert.equal(f.screeningInput.proposalVersion,f.verifiedSnapshot.documentVersionId);assert.equal(f.screeningInput.verifiedFactSnapshotId,f.verifiedSnapshot.id);}assert.equal(f.provenance.synthetic,true);assert.equal(f.provenance.notOcrOrGemini,true);assert.equal(f.verifiedSnapshot.resultOutcome,"OutputsProduced");assert.equal(f.verifiedSnapshot.snapshotOutcome,"VerifiedFacts");assert.ok(f.verifiedSnapshot.entries.every(e=>e.sourceExtractedFactId===null&&e.sourceKind==="HumanDocumentEvidence"));assert.equal("anomalyScore" in f,false);assert.equal("overallStatus" in f,false);}
 assert.throws(()=>buildDevelopmentInput("","complete"),/authorization|authorized/);
});
test("Model adapter preserves supported C3 trace events and requires a distinct DS terminal notification",()=>{
 const f=buildDevelopmentInput("assigned-synthetic-case","revised-workflow");const request=toAnomalyRequest(f.postWorkflowTrace);assert.equal(request.events.at(-1)?.activity,"DS Final Notification");assert.ok(request.events.some(x=>x.activity==="COMMISSIONER:Approved"));assert.ok(request.events.some(x=>x.activity==="PROVINCIAL_LC:Rejected"));assert.ok(f.historyAnnotations.some(x=>x.revision===2));assert.ok(request.events.every((e,i)=>e.eventSeq===i+1&&!e.timestamp.endsWith("Z")));assert.throws(()=>toAnomalyRequest({...f.postWorkflowTrace,events:f.postWorkflowTrace.events.slice(0,-1)}),/DS Final Notification/);assert.throws(()=>toAnomalyRequest({...f.postWorkflowTrace,events:f.postWorkflowTrace.events.map(e=>({...e,atUtc:e.atUtc.replace("Z","")}))}),/explicit valid UTC/);
});
test("Incomplete input stays unavailable and test coverage states are separately explicit",()=>{
 const missing=buildDevelopmentInput("assigned-synthetic-case","incomplete");assert.equal(missing.screeningInput,null);assert.ok(missing.missingInputs.some(x=>x.includes("unknown as false")));assert.ok(missing.indicators.every(x=>x.evidenceState==="Unavailable"));const concern=buildDevelopmentInput("assigned-synthetic-case","complaint");assert.ok(concern.indicators.some(x=>x.evidenceState==="VerifiedPresent"));assert.ok(concern.screeningInput!.contemporaneousComplaints[0].summary.startsWith("SYNTHETIC"));const overlap=buildDevelopmentInput("assigned-synthetic-case","overlap");assert.equal(overlap.screeningInput!.spatialEvidence?.hasOverlappingParcels,true);assert.equal(overlap.screeningInput!.concurrentApplications.length,1);
});
test("Input panel requires both local Development and explicit server opt-in",()=>{
 assert.equal(localInputPanelAllowed("production","127.0.0.1",true),false);assert.equal(localInputPanelAllowed("development","example.com",true),false);assert.equal(localInputPanelAllowed("development","localhost",false),false);assert.equal(localInputPanelAllowed("development","localhost",true),true);
});
test("History is current only for the exact backend-current assessment and snapshot",()=>{
 const item={assessmentId:"a",verifiedFactSnapshotId:"s"} as ConsolidatedHistoryItem;assert.equal(matchesCurrentAssessment(item,{isCurrent:true,verifiedFactSnapshotId:"s",latestAssessment:{assessment:{assessmentId:"a"}}}),true);assert.equal(matchesCurrentAssessment(item,{isCurrent:false,verifiedFactSnapshotId:"s",latestAssessment:{assessment:{assessmentId:"a"}}}),false);assert.equal(matchesCurrentAssessment(item,{isCurrent:true,verifiedFactSnapshotId:"other",latestAssessment:{assessment:{assessmentId:"a"}}}),false);
});

test("Actual engine responses must match the selected case",()=>{assert.throws(()=>requireMatchingEngineCase({caseId:"other"},"assigned"),/another case/);assert.equal(requireMatchingEngineCase({caseId:"assigned"},"assigned").caseId,"assigned");});

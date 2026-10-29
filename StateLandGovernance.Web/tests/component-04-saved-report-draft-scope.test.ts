import test from "node:test";
import assert from "node:assert/strict";
import {loadOwnedReportInvestigation,type RequestReport,type Investigation} from "../src/modules/component-04/services/officerFlows";

const report:RequestReport={id:"saved",creatorActorId:"responsible-ds",kind:"early",state:"Saved",sourceVersion:"original",createdAtUtc:"2026-10-24T00:00:00Z",investigationId:"original-investigation",earlyAssessmentId:"original-result",anomalyAssessmentId:null,result:null,audit:[]};
test("authorized cross-creator report reading never requires DS draft access",async()=>{
 let calls=0;const read=async()=>{calls++;throw new Error("Draft forbidden");};
 assert.equal(await loadOwnedReportInvestigation(report,"commissioner",read),null);
 assert.equal(await loadOwnedReportInvestigation(report,null,read),null);
 assert.equal(await loadOwnedReportInvestigation({...report,creatorActorId:undefined},"responsible-ds",read),null);
 assert.equal(calls,0);
});
test("responsible DS still retrieves the exact original investigation",async()=>{
 const original={id:"original-investigation"} as Investigation;const requested:string[]=[];
 assert.equal(await loadOwnedReportInvestigation(report,"responsible-ds",async id=>{requested.push(id);return original;}),original);
 assert.deepEqual(requested,["original-investigation"]);
});
test("owner investigation errors remain explicit and do not substitute history",async()=>{
 await assert.rejects(()=>loadOwnedReportInvestigation(report,"responsible-ds",async()=>{throw new Error("Original unavailable");}),/Original unavailable/);
});

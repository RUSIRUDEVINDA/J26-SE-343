import test from "node:test";
import assert from "node:assert/strict";
import {validatedActiveWorkflow,rememberActiveWorkflow,readActiveWorkflow,clearWorkflowSelection} from "../src/modules/component-04/services/currentEarlySelection";
import type {RequestReport,Investigation} from "../src/modules/component-04/services/officerFlows";
import type {RegulatoryReport} from "../src/modules/component-04/services/independentRegulatoryReports";
const early={id:"early",creatorActorId:"ds",kind:"early",state:"Saved",investigationId:"investigation",earlyAssessmentId:"assessment"} as RequestReport;
const investigation={id:"investigation",proposalId:"proposal"} as Investigation;
const regulatory={metadata:{id:"regulatory",proposalId:"proposal"},provenance:{creatorActorId:"ds",earlyRequestId:"early"}} as RegulatoryReport;
const selection={earlyRequestId:"early",regulatoryEvaluationId:"regulatory",conflictReportId:null};
function storage(){const data=new Map<string,string>();return {getItem:(k:string)=>data.get(k)??null,setItem:(k:string,v:string)=>{data.set(k,v);},removeItem:(k:string)=>{data.delete(k);}};}
test("authorized deep link preserves all identities through sidebar Early Regulatory Early and refresh",()=>{
 const store=storage();const linked=readActiveWorkflow("ds",new URLSearchParams("earlyRequestId=early&regulatoryEvaluationId=regulatory"),store);
 rememberActiveWorkflow("ds",validatedActiveWorkflow("ds",linked,early,investigation,regulatory,"proposal"),store);
 for(const navigation of ["sidebar Early","sidebar Regulatory","sidebar Early","refresh"])assert.deepEqual(readActiveWorkflow("ds",new URLSearchParams(),store),{...selection,proposalId:"proposal",investigationId:"investigation",earlyAssessmentId:"assessment"},navigation);
});
test("deliberate Start Screening overrides and clears active selection",()=>{const store=storage();rememberActiveWorkflow("ds",selection,store);assert.equal(readActiveWorkflow("ds",new URLSearchParams("freshWorkflow=fresh"),store).earlyRequestId,null);clearWorkflowSelection("ds",store);assert.equal(readActiveWorkflow("ds",new URLSearchParams(),store).regulatoryEvaluationId,null);});
test("actor selection is isolated and unauthorized owner fails closed",()=>{const store=storage();rememberActiveWorkflow("ds",selection,store);assert.equal(readActiveWorkflow("other",new URLSearchParams(),store).earlyRequestId,null);assert.throws(()=>validatedActiveWorkflow("other",selection,early,investigation,regulatory,"proposal"),/officer/);});
test("stale Early and Regulatory IDs and unrelated findings cannot become active context",()=>{
 assert.throws(()=>validatedActiveWorkflow("ds",{...selection,earlyRequestId:"stale"},early,investigation,regulatory,"proposal"));
 assert.throws(()=>validatedActiveWorkflow("ds",{...selection,regulatoryEvaluationId:"stale"},early,investigation,regulatory,"proposal"));
 assert.throws(()=>validatedActiveWorkflow("ds",selection,{...early,state:"Pending"},investigation,regulatory,"proposal"));
 assert.throws(()=>validatedActiveWorkflow("ds",selection,early,{...investigation,id:"unrelated"},regulatory,"proposal"));
 assert.throws(()=>validatedActiveWorkflow("ds",selection,early,{...investigation,proposalId:"unrelated"},regulatory,"proposal"));
 assert.throws(()=>validatedActiveWorkflow("ds",selection,early,investigation,{...regulatory,provenance:{...regulatory.provenance!,earlyRequestId:"unrelated"}},"proposal"));
});
test("corrupt local context fails closed and Early-only links retain saved identity",()=>{const store=storage();store.setItem("c4-active-workflow/1/ds","{invalid");assert.equal(readActiveWorkflow("ds",new URLSearchParams(),store).earlyRequestId,null);const context=validatedActiveWorkflow("ds",{...selection,regulatoryEvaluationId:null},early,investigation,null,"proposal");assert.equal(context.investigationId,"investigation");assert.equal(context.regulatoryEvaluationId,null);});

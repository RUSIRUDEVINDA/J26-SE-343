import {test} from "node:test";
import assert from "node:assert/strict";
import {kiwulaMessage,stableKiwulaKey} from "../src/modules/component-04/services/kiwulaAssessments";
import {createPostWorkflowScope} from "../src/modules/component-04/services/postWorkflowDemonstrations";
test("Uncertain submissions and reloads preserve a key; other actor and engine have separate keys",()=>{
 const values=new Map<string,string>();const storage={getItem:(k:string)=>values.get(k)??null,setItem:(k:string,v:string)=>{values.set(k,v);}};let n=0;const key=()=>String(++n);
 assert.equal(stableKiwulaKey("owner","conflict",storage,key),"1");assert.equal(stableKiwulaKey("owner","conflict",storage,key),"1");assert.equal(stableKiwulaKey("other","conflict",storage,key),"2");assert.equal(stableKiwulaKey("owner","early",storage,key),"3");assert.equal(n,3);
});
test("Account switch aborts pending assessment retrieval and cannot restore old private results",async()=>{const scope=createPostWorkflowScope();const old=scope.capture();let report="";const pending=Promise.resolve("private").then(r=>{if(scope.current(old.epoch))report=r;});scope.invalidate();await pending;assert.equal(report,"");assert.equal(old.signal.aborted,true);});
test("Submission failures and missing deployment remain actionable and never imply success",()=>{assert.match(kiwulaMessage(503),/controlled activation/);assert.match(kiwulaMessage(403),/cannot access/);assert.match(kiwulaMessage(401),/Sign in/);assert.match(kiwulaMessage(409),/different input/);assert.match(kiwulaMessage(0),/same submission/);});

test("Saved screening selection excludes other proposals and input versions without altering history",async()=>{
 const {savedKiwulaAssessment}=await import("../src/modules/component-04/services/kiwulaAssessments");
 type Row=Parameters<typeof savedKiwulaAssessment>[0][number];
 const rows=[{id:"matching",engine:"conflict",proposalId:"proposal",inputVersion:"current",createdAtUtc:"2026-01-01"},{id:"other-version",engine:"conflict",proposalId:"proposal",inputVersion:"old",createdAtUtc:"2026-02-01"},{id:"other-proposal",engine:"conflict",proposalId:"other",inputVersion:"current",createdAtUtc:"2026-03-01"}] as Row[];
 assert.equal(savedKiwulaAssessment(rows,"conflict","proposal","current")?.id,"matching");
 assert.equal(savedKiwulaAssessment(rows,"early","proposal","current"),undefined);
 assert.deepEqual(rows.map(r=>r.id),["matching","other-version","other-proposal"]);
});

test("Deliberate new submissions retain uncertain keys across reload and rotate only after completion",async()=>{
 const {deliberateKiwulaKey,completeKiwulaSubmission}=await import("../src/modules/component-04/services/kiwulaAssessments");
 const values=new Map<string,string>();const storage={getItem:(k:string)=>values.get(k)??null,setItem:(k:string,v:string)=>{values.set(k,v);},removeItem:(k:string)=>{values.delete(k);}};let n=0;const make=()=>String(++n);
 assert.equal(deliberateKiwulaKey("a","early",storage,make),"1");assert.equal(deliberateKiwulaKey("a","early",storage,make),"1");
 assert.equal(deliberateKiwulaKey("b","early",storage,make),"2");completeKiwulaSubmission("a","early",storage);
 assert.equal(deliberateKiwulaKey("a","early",storage,make),"3");
});

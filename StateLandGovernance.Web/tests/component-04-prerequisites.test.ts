import test from "node:test";
import assert from "node:assert/strict";
import { checkScreeningPrerequisites, type PrerequisiteFeedback } from "../src/modules/component-04/services/screeningPrerequisites";

const ready = { caseId:"FICTIONAL-CASE", caseRevision:1, status:"ReadyForScreening", verifiedFactSnapshotId:"FICTIONAL-SNAPSHOT", missingInputs:[] };

test("Prerequisites publish loading until the actual GET response resolves", async()=>{
  const updates:PrerequisiteFeedback[]=[];
  let resolve!:(value:unknown)=>void;
  const response=new Promise<unknown>(done=>{resolve=done;});
  const check=checkScreeningPrerequisites(()=>response, feedback=>updates.push(feedback));
  assert.deepEqual(updates,[{kind:"loading",message:"Checking prerequisites…"}]);
  resolve(ready); await check;
  assert.equal(updates.at(-1)?.message,"Ready to screen");
});

test("Returned blockers take precedence over a ready status", async()=>{
  const updates:PrerequisiteFeedback[]=[];
  await checkScreeningPrerequisites(async()=>({...ready,missingInputs:["Publish a verified snapshot.","Document completeness is not current."]}), feedback=>updates.push(feedback));
  assert.equal(updates.at(-1)?.kind,"blocked");
  assert.match(updates.at(-1)!.message,/Publish a verified snapshot/);
  assert.match(updates.at(-1)!.message,/Document completeness is not current/);
});

test("Empty, missing and malformed responses never confirm readiness", async()=>{
  for(const response of [undefined,null,"",{}, {...ready,missingInputs:undefined}, {...ready,status:undefined}]){
    const updates:PrerequisiteFeedback[]=[];
    await assert.rejects(checkScreeningPrerequisites(async()=>response, feedback=>updates.push(feedback)));
    assert.equal(updates.at(-1)?.kind,"error");
    assert.ok(!updates.some(update=>update.kind==="ready"));
  }
});

test("Empty blocker arrays alone do not establish readiness", async()=>{
  for(const status of ["Pending","Stale","Assessed","BlockedIncomplete"]){
    const updates:PrerequisiteFeedback[]=[];
    await checkScreeningPrerequisites(async()=>({...ready,status}), feedback=>updates.push(feedback));
    assert.equal(updates.at(-1)?.kind,"unconfirmed");
  }
  const updates:PrerequisiteFeedback[]=[];
  await checkScreeningPrerequisites(async()=>({...ready,verifiedFactSnapshotId:undefined}), feedback=>updates.push(feedback));
  assert.equal(updates.at(-1)?.kind,"unconfirmed");
});

test("Failed requests retain useful errors and correlation references", async()=>{
  const updates:PrerequisiteFeedback[]=[];
  await assert.rejects(checkScreeningPrerequisites(async()=>{throw new Error("Service unavailable. Error reference: TEST-CORRELATION-001");},feedback=>updates.push(feedback)),/TEST-CORRELATION-001/);
  assert.deepEqual(updates.map(update=>update.kind),["loading","error"]);
  assert.match(updates.at(-1)!.message,/TEST-CORRELATION-001/);
});

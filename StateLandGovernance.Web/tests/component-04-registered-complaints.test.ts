import {test} from "node:test";
import assert from "node:assert/strict";
import {classificationMessage,canRetry,submissionKey,clearSubmissionKey,type Complaint} from "../src/modules/component-04/services/registeredComplaints";
const complaint={classificationState:"Failed",attempts:[]} as unknown as Complaint;
test("ML failure remains registered and never claims verification",()=>{
 assert.match(classificationMessage(complaint),/Complaint saved; classification failed/);
 assert.match(classificationMessage({...complaint,classificationState:"Classified"}),/Allegations remain unverified/);
 assert.equal(canRetry({...complaint,classificationState:"Classified"}),false);
});
test("Interrupted classification waits for its lease and retries the saved record",()=>{
 const c={...complaint,classificationState:"InProgress",attempts:[{startedAtUtc:"2026-10-21T00:00:00Z"}]} as Complaint;
 assert.equal(canRetry(c,Date.parse("2026-10-21T00:04:59Z")),false);
 assert.equal(canRetry(c,Date.parse("2026-10-21T00:05:00Z")),true);
 assert.equal(canRetry({...c,attempts:[]}),false);
});
test("Submission retry preserves idempotency key until acknowledged",()=>{
 const values=new Map<string,string>();
 Object.defineProperty(globalThis,"sessionStorage",{configurable:true,value:{getItem:(k:string)=>values.get(k)??null,setItem:(k:string,v:string)=>values.set(k,v),removeItem:(k:string)=>values.delete(k)}});
 const first=submissionKey();assert.equal(submissionKey(),first);clearSubmissionKey();assert.notEqual(submissionKey(),first);
});

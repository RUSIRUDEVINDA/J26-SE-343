import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {analysisMessage,createPostWorkflowScope,postWorkflowError,type DemoReport} from "../src/modules/component-04/services/postWorkflowDemonstrations";
test("Pending and failed submissions never imply a model outcome",()=>{
 assert.match(analysisMessage({state:"Pending"} as DemoReport),/interrupted/);
 assert.match(analysisMessage({state:"Failed"} as DemoReport),/No prediction/);
 assert.match(analysisMessage({state:"Saved"} as DemoReport),/Actual model result saved/);
});
test("Endpoint deployment, activation, auth, storage and network errors stay distinct",()=>{
 assert.match(postWorkflowError(404,{}),/running API.*HTTP 404/);
 assert.match(postWorkflowError(404,{},true),/Saved report not found/);
 assert.match(postWorkflowError(401,{}),/Sign in/);assert.match(postWorkflowError(403,{}),/Access denied/);
 assert.match(postWorkflowError(503,{title:"Development post-workflow demonstration is not activated"}),/not activated/);
 assert.match(postWorkflowError(503,{title:"Post-workflow persistence unavailable",traceId:"test-reference"}),/persistence.*Reference: test-reference/);
 assert.match(postWorkflowError(0,{}),/unreachable/);
});
test("Account switching aborts old requests and rejects all stale responses",async()=>{
 const scope=createPostWorkflowScope();const old=scope.capture();let displayed="previous-account";
 const response=Promise.resolve("private old report").then(value=>{if(scope.current(old.epoch))displayed=value;});
 scope.invalidate();displayed="";await response;
 assert.equal(old.signal.aborted,true);assert.equal(displayed,"");assert.equal(scope.current(old.epoch),false);
 const next=scope.capture();assert.equal(scope.current(next.epoch),true);assert.equal(next.signal.aborted,false);
 scope.invalidate();assert.equal(scope.current(next.epoch),false);
});
test("Post-workflow screen uses actual authenticated retrieval and deliberate execution",()=>{
 const service=readFileSync(new URL("../src/modules/component-04/services/postWorkflowDemonstrations.ts",import.meta.url),"utf8");
 const view=readFileSync(new URL("../src/modules/component-04/components/PostWorkflowDemonstrations.tsx",import.meta.url),"utf8");
 assert.match(service,/readDevelopmentSession/);assert.match(service,/cache:"no-store"/);
 assert.match(view,/scores are not probabilities/);assert.match(view,/does not prove corruption/);
 assert.match(view,/DS Final Notification/);assert.match(view,/Open saved report/);
 assert.doesNotMatch(service,/fixture|mock|predict-workflow-anomaly/);
});

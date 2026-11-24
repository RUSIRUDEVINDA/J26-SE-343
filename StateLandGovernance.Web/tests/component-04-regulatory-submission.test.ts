import {ApiClientError} from "../src/lib/apiClient";
import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {validateRegulatoryDraft,isCorrectableRegulatoryError,regulatorySubmissionKey,savePending,pendingRequest,clearPending,requireSavedRegulatoryReport,type RegulatoryInput,type RegulatoryRequest} from "../src/modules/component-04/services/regulatorySubmission";
import type {RegulatoryReport} from "../src/modules/component-04/services/independentRegulatoryReports";
const source=JSON.parse(readFileSync(new URL("../src/modules/component-04/evidence/kiulakale-regulatory-input.json",import.meta.url),"utf8")) as RegulatoryInput;
test("Loading prepared input preserves unknowns and cannot create a case binding",()=>{
 const draft=structuredClone(source);assert.equal((draft.location as RegulatoryInput).province,null);assert.equal((draft.stakeholders as RegulatoryInput).expectedStakeholders,null);assert.equal(draft.preliminaryAssessment,null);
 (draft.location as RegulatoryInput).district="Edited";assert.notEqual((source.location as RegulatoryInput).district,"Edited");assert.equal(draft.caseId,undefined);
 assert.deepEqual(validateRegulatoryDraft(source,"v1","Demonstration assumptions"),[]);
 assert.ok(validateRegulatoryDraft({proposalId:""},"","").length>=3);
});
test("Pending submission is frozen for same-key retries and isolated by signed-in actor",()=>{
 const values=new Map<string,string>();const storage={getItem:(k:string)=>values.get(k)??null,setItem:(k:string,v:string)=>values.set(k,v),removeItem:(k:string)=>values.delete(k)};
 Object.defineProperty(globalThis,"sessionStorage",{configurable:true,value:storage});Object.defineProperty(globalThis,"window",{configurable:true,value:{sessionStorage:storage}});
 function login(actor:string){storage.setItem("slg.localDevelopmentSession",JSON.stringify({accessToken:"test-only",expiresAtUtc:new Date(Date.now()+60000).toISOString(),user:{actorId:actor}}));}
 login("actor-a");const request={actionName:"test",input:structuredClone(source),submissionKey:"same-key",inputVersion:"v1",schemaVersion:"c4-regulatory-input/1",sourceReference:"Assumptions",c3Binding:null} as RegulatoryRequest;
 savePending(request);request.input.proposalId="edited after pending";assert.equal(pendingRequest()!.submissionKey,"same-key");assert.notEqual(pendingRequest()!.input.proposalId,"edited after pending");
 login("actor-b");assert.equal(pendingRequest(),null);login("actor-a");assert.equal(pendingRequest()!.submissionKey,"same-key");clearPending();assert.equal(pendingRequest(),null);
 assert.throws(()=>requireSavedRegulatoryReport({} as unknown as RegulatoryReport),/not confirmed/);
 assert.throws(()=>requireSavedRegulatoryReport({metadata:{id:"x"},findings:[],provenance:{creatorActorId:"actor-b"}} as unknown as RegulatoryReport),/not confirmed/);
 const report={metadata:{id:"x"},findings:[],provenance:{creatorActorId:"actor-a"}} as unknown as RegulatoryReport;assert.equal(requireSavedRegulatoryReport(report),report);
});

test("Definitive validation/denial allows correction with the same key; uncertain failures stay frozen",()=>{
 assert.equal(isCorrectableRegulatoryError(new ApiClientError({status:400,title:"Invalid",detail:""})),true);
 assert.equal(isCorrectableRegulatoryError(new ApiClientError({status:403,title:"Denied",detail:""})),true);
 assert.equal(isCorrectableRegulatoryError(new ApiClientError({status:500,title:"Unknown outcome",detail:""})),false);
 assert.equal(isCorrectableRegulatoryError(new Error("Lost response")),false);
 assert.equal(regulatorySubmissionKey({submissionKey:"retained-key"} as RegulatoryRequest),"retained-key");
});

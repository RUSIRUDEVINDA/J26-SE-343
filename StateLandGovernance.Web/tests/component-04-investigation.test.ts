import assert from "node:assert/strict";
import test from "node:test";
import {investigationError,conservativeEvidenceState,investigationConcerns} from "../src/modules/component-04/services/earlyInvestigation";
test("Officer observations never promote themselves to verified absence or presence",()=>{
 assert.equal(conservativeEvidenceState({choice:"NoIssueFound",remarks:""}),"Unverified");
 assert.equal(conservativeEvidenceState({choice:"IssueFound",remarks:"A concern was reported by the person interviewed."}),"Unverified");
 assert.equal(conservativeEvidenceState({choice:"NotYetVerified",remarks:""}),"Missing");
 assert.equal(conservativeEvidenceState({choice:"NotYetVerified",remarks:"Investigation requested."}),"Unverified");
});
test("Issue Found requires substantive remarks; the form retains exactly seven distinct concerns",()=>{
 assert.ok(investigationError({choice:"IssueFound",remarks:"yes"}));
 assert.equal(investigationError({choice:"IssueFound",remarks:"A concern was reported during the site visit."}),"");
 assert.equal(investigationConcerns.length,7);assert.equal(new Set(investigationConcerns.map(([key])=>key)).size,7);
});

test("Prepared named workflow preserves chronological ordering and a separate terminal notification",async()=>{
 const {readFile}=await import("node:fs/promises");
 const data=JSON.parse(await readFile(new URL("../src/modules/component-04/evidence/kiwula-named-workflow-v2.json",import.meta.url),"utf8"));
 assert.deepEqual(data.events.map((e:{eventSeq:number})=>e.eventSeq),[1,2,3,4,5,6,7]);
 assert.equal(data.events.at(-1).activity,"DS Final Notification");assert.equal(data.events.at(-2).activity,"Commissioner Decision");
 for(let i=1;i<data.events.length;i++)assert.ok(Date.parse(data.events[i].timestamp)>=Date.parse(data.events[i-1].timestamp));
 assert.equal(data.sourceVersion,"kiwula-named-route/2");assert.equal(data.events.some((e:{institutionName:string})=>e.institutionName.includes("EXAMPLE")),false);
 assert.equal("anomalyScore" in data,false);
});

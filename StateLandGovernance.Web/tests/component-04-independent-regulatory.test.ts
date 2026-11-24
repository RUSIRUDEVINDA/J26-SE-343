import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {regulatoryCounts,regulatoryGroup,filterRegulatoryFindings,regulatoryError,type RegulatoryFinding} from "../src/modules/component-04/services/independentRegulatoryReports";
import {ApiClientError} from "../src/lib/apiClient";
const actual=JSON.parse(readFileSync(new URL("./fixtures/independent-regulatory-findings.json",import.meta.url),"utf8")) as RegulatoryFinding[];
test("Actual persisted24-rule response retains every finding and observed count",()=>{
 assert.equal(actual.length,24);assert.deepEqual(regulatoryCounts(actual),{InsufficientInformation:7,Compliant:14,RequiresHumanReview:3});
 assert.equal(filterRegulatoryFindings(actual,"","").length,24);
 assert.equal(new Set(actual.map(f=>f.ruleCode)).size,24);
 assert.ok(actual.every(f=>f.observedValueSummary&&f.sourceDocument&&f.sourceSection&&f.recommendedAction));
});
test("Filters retain exact returned findings and sources",()=>{
 assert.equal(filterRegulatoryFindings(actual,"InsufficientInformation","").length,7);
 const match=filterRegulatoryFindings(actual,"","stakeholder");assert.ok(match.length>0);assert.ok(match.every(f=>actual.includes(f)));
 assert.equal(filterRegulatoryFindings(actual,"","unmatched phrase").length,0);
});
test("Unresolved and explicit NotApplicable never become satisfied checks",()=>{
 const f={...actual[0],isBlocking:false,requiresHumanReview:false};
 assert.equal(regulatoryGroup({...f,status:"InsufficientInformation"}),"Unresolved");
 assert.equal(regulatoryGroup({...f,status:"NotApplicable",applicability:"NotApplicable"}),"Not applicable");
 assert.equal(regulatoryGroup({...f,status:"NotApplicable",isBlocking:true}),"Issues");
 assert.equal(regulatoryGroup({...f,status:"FutureUnknownStatus"}),"Other returned findings");
});
test("Sign-in, denial, missing and service errors remain distinct",()=>{
 assert.match(regulatoryError(new ApiClientError({status:401,title:"",detail:""})),/Sign in/);
 assert.match(regulatoryError(new ApiClientError({status:403,title:"",detail:""})),/Access denied/);
 assert.match(regulatoryError(new ApiClientError({status:404,title:"",detail:""})),/not found/);
 assert.match(regulatoryError(new Error("Unavailable. Reference abc")),/Reference abc/);
});

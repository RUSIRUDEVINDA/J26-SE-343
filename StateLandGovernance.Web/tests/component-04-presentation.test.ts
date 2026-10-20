import {test} from "node:test";
import assert from "node:assert/strict";
import {findingGroup,selectAssignedCase} from "../src/modules/component-04/services/reportPresentation";
test("Persisted case context never selects an unassigned requested or saved case",()=>{
 assert.equal(selectAssignedCase(["assigned-a","assigned-b"],"other-case","assigned-b"),"assigned-b");
 assert.equal(selectAssignedCase(["assigned-a"],null,"revoked-case"),"assigned-a");
 assert.equal(selectAssignedCase([],"other-case","revoked-case"),"");
});
test("Explicit assigned case navigation takes precedence over prior context",()=>{
 assert.equal(selectAssignedCase(["a","b"],"a","b"),"a");
});
test("Blocking findings remain issues even when human review is also required",()=>{
 assert.equal(findingGroup({isBlocking:true,requiresHumanReview:true}),"Issues");
 assert.equal(findingGroup({isBlocking:false,requiresHumanReview:true}),"Review items");
 assert.equal(findingGroup({isBlocking:false,requiresHumanReview:false}),"Satisfied / advisory");
});

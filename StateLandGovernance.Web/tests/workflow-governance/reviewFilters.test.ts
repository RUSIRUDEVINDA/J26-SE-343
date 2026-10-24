import {test} from "node:test";
import assert from "node:assert/strict";
import {matchesFactFilter, matchesObservationFilter} from "../../src/modules/workflow-governance/services/reviewFilters";
test("Outstanding facts include absent candidates, but exclude persisted decisions",()=>{
 assert.equal(matchesFactFilter("NoCandidate","Needs review"),true);
 assert.equal(matchesFactFilter("Extracted","Needs review"),true);
 assert.equal(matchesFactFilter("Confirmed","Needs review"),false);
 assert.equal(matchesFactFilter("Rejected","Reviewed"),true);
 assert.equal(matchesFactFilter("Unsupported","Reviewed"),true);
 assert.equal(matchesFactFilter("Ungrounded","Missing"),false);
 assert.equal(matchesFactFilter("Unavailable","Missing"),false);
});
test("Present machine content is never presented as individually reviewed",()=>{
 assert.equal(matchesObservationFilter("Present","Reviewed"),false);
 assert.equal(matchesObservationFilter("Uncertain","Needs review"),true);
 assert.equal(matchesObservationFilter("Unreadable","Missing"),false);
 assert.equal(matchesObservationFilter("Missing","Missing"),true);
});

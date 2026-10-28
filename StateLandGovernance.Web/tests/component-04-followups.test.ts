import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {RegulatoryFollowups} from "../src/modules/component-04/components/RegulatoryFollowups";
import {followupOperations,followupRevision,humanReviewOutcomes} from "../src/modules/component-04/services/regulatoryFollowups";
import type {RegulatoryFinding} from "../src/modules/component-04/services/independentRegulatoryReports";
test("Follow-up actions depend only on saved finding status",()=>{
 const f={status:"Compliant",requiresHumanReview:false} as RegulatoryFinding;
 assert.deepEqual(followupOperations(f),["NoFollowupRequired"]);assert.deepEqual(followupOperations({...f,status:"InsufficientInformation"}),["ClarificationRequested","FindingVerifiedClosed"]);
 assert.deepEqual(followupOperations({...f,status:"RequiresHumanReview"}),["HumanReviewRecorded","FindingVerifiedClosed"]);
 assert.equal(followupRevision([],"RULE"),0);assert.equal(humanReviewOutcomes.length,3);assert.ok(!humanReviewOutcomes.some(x=>String(x)==="Approved"));
});
test("Follow-up workspace keeps original results separate and never auto-submits",()=>{
 const html=renderToStaticMarkup(createElement(RegulatoryFollowups,{evaluationId:"saved",findings:[]}));
 assert.match(html,/Original findings remain unchanged/);assert.match(html,/does not deliver a request/);assert.doesNotMatch(html,/<form/);
 const source=readFileSync("src/modules/component-04/components/RegulatoryFollowups.tsx","utf8");
 assert.match(source,/followupOperationLabel/);assert.match(source,/Replay Same Follow-up Request/);assert.match(source,/sessionStorage.setItem/);
 assert.doesNotMatch(source,/runRegulatory|setInterval/);assert.match(source,/development-session-changed/);
});

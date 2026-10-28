import {test} from "node:test";
import assert from "node:assert/strict";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {RegulatoryHistoryCard,groupPreviousFollowups} from "../src/modules/component-04/components/PreviousRegulatoryFollowups";
import {RegulatoryFollowupEntries} from "../src/modules/component-04/components/RegulatoryFollowupHistory";
import type {FollowupEvent} from "../src/modules/component-04/services/regulatoryFollowups";
import type {RegulatoryFinding} from "../src/modules/component-04/services/independentRegulatoryReports";
const event:FollowupEvent={id:"original-event-a",evaluationId:"original-assessment",ruleCode:"RULE-1",ruleVersion:"1",actorId:"original-officer",createdAtUtc:"2026-10-23T00:00:00Z",operation:"ClarificationRequested",reviewOutcome:null,missingInformation:"Original missing information description.",remarks:"Original officer clarification remarks.",references:["Document reference A"],status:"Recorded",revision:1,previousEventId:null,eventSha256:"original-event-sha"};
const finding={ruleCode:"RULE-1",ruleVersion:"1",expectedRequirement:"Original saved rule requirement",status:"InsufficientInformation",category:"Legal"} as RegulatoryFinding;
test("Regulatory cards show original finding and preserve multiple revisions without editing",()=>{
 const second={...event,id:"original-event-b",revision:2,operation:"HumanReviewRecorded",reviewOutcome:"AdditionalInformationRequired",previousEventId:event.id};
 const group=groupPreviousFollowups([{evaluationId:event.evaluationId,evaluationTimestamp:event.createdAtUtc,event},{evaluationId:event.evaluationId,evaluationTimestamp:event.createdAtUtc,event:second}])[0];
 const html=renderToStaticMarkup(createElement(RegulatoryHistoryCard,{group,findings:[finding]}));
 for(const text of ["Original saved rule requirement","Initial Outcome:","Insufficient information","Request Clarification","Record Human Review","revision 1","revision 2","Original missing information description.","Original officer clarification remarks.","Document reference A","original-officer","original-event-sha","/component-04/regulatory-assessments/original-assessment"])assert.ok(html.includes(text),text);
 assert.equal((html.match(/<details/g)||[]).length,1);assert.match(html,/<summary>Technical details<\/summary>/);assert.doesNotMatch(html,/<form|<textarea|<input|Historical — Read-only/);
 assert.deepEqual(group.events.map(x=>x.id),["original-event-a","original-event-b"]);
});
test("A different rule version never supplies a fabricated original outcome",()=>{
 const html=renderToStaticMarkup(createElement(RegulatoryFollowupEntries,{events:[event],findings:[{...finding,ruleVersion:"2",status:"Compliant"}]}));
 assert.match(html,/Unavailable in the loaded report/);assert.doesNotMatch(html,/Original saved rule requirement|Compliant/);
});
test("No original report metadata is invented when only historical events are available",()=>{
 const html=renderToStaticMarkup(createElement(RegulatoryFollowupEntries,{events:[event]}));assert.match(html,/RULE-1/);assert.match(html,/Unavailable in the loaded report/);assert.match(html,/Original officer clarification remarks/);
});

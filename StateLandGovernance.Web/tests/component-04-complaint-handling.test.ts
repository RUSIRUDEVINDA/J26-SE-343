import {test} from "node:test";
import assert from "node:assert/strict";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {ComplaintHandlingPanel,DsRoutingReason} from "../src/modules/component-04/components/ComplaintHandlingPanel";
import {evidenceReferences,type HandlingView} from "../src/modules/component-04/services/complaintHandling";

const view:HandlingView={state:{revision:4,status:"UnderInvestigation",mode:"Ds",responsibleActorId:null,assignmentEventId:null,source:"HandlingEvent"},allowedActions:["RecordNote"],history:[]};
test("DS sees the latest saved Commissioner routing reason without exposing redacted or older reasons",()=>{
 const event={id:"assignment",revision:1,operation:"AssignDs",status:"AssignedToDs",atUtc:"2026-10-26T00:00:00Z",publicSummary:"Assigned",actorId:null,reason:"Old routing",details:null,evidenceReferences:null};
 const assigned:HandlingView={...view,history:[{...event,id:"latest",revision:3,operation:"ReassignDs",reason:"Investigate irrigation access"},event]};
 const render=(v:HandlingView,central=false)=>renderToStaticMarkup(createElement(DsRoutingReason,{view:v,central}));
 assert.match(render(assigned),/Commissioner routing reason/);
 assert.match(render(assigned),/Investigate irrigation access/);
 assert.doesNotMatch(render(assigned),/Old routing/);
 assert.equal(render(assigned,true),"");
 assert.equal(render({...assigned,history:[{...event,reason:null}]}),"");
 assert.equal(render({...assigned,state:{...view.state,mode:"None"}}),"");
});
test("Handling UI offers only operations returned by the server",()=>{
 const html=renderToStaticMarkup(createElement(ComplaintHandlingPanel,{id:"isolated",view,central:false,onSaved:async()=>{}}));
 assert.match(html,/value="RecordNote"/);assert.doesNotMatch(html,/value="Resolve"|value="Reopen"|value="TakeDirect"/);
});
test("Applicant-safe read-only handling history does not invent write controls or findings",()=>{
 const safe:HandlingView={...view,state:{...view.state,status:"Resolved",mode:"None"},allowedActions:[],history:[{id:"saved",revision:5,operation:"Resolve",status:"Resolved",atUtc:"2026-10-23T00:00:00Z",publicSummary:"Unable to determine on available evidence.",actorId:null,reason:null,details:null,evidenceReferences:null}]};
 const html=renderToStaticMarkup(createElement(ComplaintHandlingPanel,{id:"isolated",view:safe,central:false,onSaved:async()=>{}}));
 assert.match(html,/Unable to determine on available evidence/);assert.doesNotMatch(html,/<form|<textarea|Verified allegation/);
});
test("Evidence entry preserves references as data and rejects malformed or excessive entries",()=>{
 assert.deepEqual(evidenceReferences("ExternalReference | actual correspondence reference | Officer supplied label"),[{kind:"ExternalReference",reference:"actual correspondence reference",label:"Officer supplied label"}]);
 assert.throws(()=>evidenceReferences("Upload | secret | invalid kind"));
 assert.throws(()=>evidenceReferences("C4Record | id | label | extra"));
 assert.throws(()=>evidenceReferences(Array.from({length:21},()=>"ExternalReference | actual | label").join("\n")));
 assert.deepEqual(evidenceReferences(""),[]);
});

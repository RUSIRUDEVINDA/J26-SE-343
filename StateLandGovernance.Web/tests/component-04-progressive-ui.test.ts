import assert from "node:assert/strict";
import test from "node:test";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {ProgressiveSteps,ProgressiveStep,StatusBadge} from "../src/modules/component-04/components/ProgressiveSteps";
import {statusLabel,statusTone,sriLankaTime} from "../src/modules/component-04/services/presentation";

test("Incomplete and review states never become successful findings",()=>{
 for(const state of ["Missing","Unverified","InsufficientInformation","NotApplicable","RequiresHumanReview","Unknown"]){
  assert.notEqual(statusTone(state),"positive");
  const html=renderToStaticMarkup(createElement(StatusBadge,{status:state}));
  assert.doesNotMatch(html,/Clear|Passed|Approved/);assert.ok(html.includes(statusLabel(state)));
 }
 assert.equal(statusTone("NoIssueFound"),"neutral");assert.equal(statusTone("Compliant"),"positive");
});
test("Native progressive expanders preserve supplied findings and details",()=>{
 const html=renderToStaticMarkup(createElement(ProgressiveSteps,{label:"Returned findings"},[
  createElement(ProgressiveStep,{key:"one",title:"Land evidence",status:"Missing"},createElement("p",null,"Request actual evidence")),
  createElement(ProgressiveStep,{key:"two",title:"Declaration",status:"NotApplicable"})
 ]));
 assert.match(html,/<ol[^>]*aria-label="Returned findings"/);assert.equal((html.match(/<li /g)||[]).length,2);
 assert.match(html,/<details><summary/);assert.match(html,/Request actual evidence/);assert.match(html,/Not applicable/);
});
test("Investigation dropdown remains outside the remarks expander summary",()=>{
 const html=renderToStaticMarkup(createElement(ProgressiveStep,{
  title:createElement("label",null,"Legal dispute",createElement("select",{"aria-label":"Legal dispute finding"},createElement("option",null,"No Issue Found"))),
  status:"Unverified",expandable:false},createElement("details",null,createElement("summary",null,"Remarks"),createElement("textarea",{"aria-label":"Legal dispute remarks"}))
 ));
 assert.match(html,/Unverified/);assert.ok(html.indexOf("<select")<html.indexOf("<summary"));assert.match(html,/aria-label="Legal dispute remarks"/);
});
test("Scenario event labels stay neutral and times explicitly use Sri Lanka",()=>{
 assert.equal(statusTone("Conditional approval"),"neutral");assert.equal(statusLabel("A source status"),"A source status");
 assert.match(sriLankaTime("2026-02-02T03:30:00Z"),/09:00/);
});

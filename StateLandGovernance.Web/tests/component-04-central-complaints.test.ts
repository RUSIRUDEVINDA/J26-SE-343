import {test} from "node:test";
import assert from "node:assert/strict";
import {centralComplaintsApi as api} from "../src/modules/component-04/services/centralComplaints";

test("Central reads use the current portal session, no-store GET and never trigger classification",async()=>{
 const originalFetch=globalThis.fetch;const oldWindow=Object.getOwnPropertyDescriptor(globalThis,"window");
 let token="central-session";const requests:{url:string;init:RequestInit}[]=[];
 Object.defineProperty(globalThis,"window",{configurable:true,value:{location:{hostname:"localhost"},sessionStorage:{getItem:()=>JSON.stringify({accessToken:token,expiresAtUtc:new Date(Date.now()+60000).toISOString()}),removeItem:()=>{}}}});
 globalThis.fetch=async(input,init)=>{requests.push({url:String(input),init:init??{}});return new Response(JSON.stringify({canRead:true,canAssign:false}),{status:200});};
 try{
  await api.capabilities();token="changed-session";await api.detail("test-id");await api.history("test-id");await api.list("unassigned");
  assert.equal(requests.length,4);assert.ok(requests.every(x=>x.init.cache==="no-store"&&(x.init.method??"GET")==="GET"));
  assert.equal(new Headers(requests[0].init.headers).get("Authorization"),"Bearer central-session");
  assert.equal(new Headers(requests[1].init.headers).get("Authorization"),"Bearer changed-session");
  assert.ok(requests.every(x=>!x.url.includes("classification")&&!x.url.includes("predict")));
 }finally{globalThis.fetch=originalFetch;if(oldWindow)Object.defineProperty(globalThis,"window",oldWindow);else Reflect.deleteProperty(globalThis,"window");}
});

test("Assignment transport preserves an uncertain request key/body and exposes a conflict",async()=>{
 const originalFetch=globalThis.fetch;const oldWindow=Object.getOwnPropertyDescriptor(globalThis,"window");
 Object.defineProperty(globalThis,"window",{configurable:true,value:{location:{hostname:"localhost"},sessionStorage:{getItem:()=>null,removeItem:()=>{}}}});
 const bodies:string[]=[];let fail=true;
 globalThis.fetch=async(_input,init)=>{bodies.push(String(init?.body));if(fail){fail=false;throw new Error("uncertain network outcome");}return new Response(JSON.stringify({status:409,title:"Assignment changed",detail:"Refresh current assignment"}),{status:409});};
 try{
  const input={expectedHandlingRevision:0,submissionKey:"stable-key",expectedAssignmentRevision:1,targetActorId:"selected-ds",reason:"Manual routing"};
  await assert.rejects(()=>api.assign("complaint",input));await assert.rejects(()=>api.assign("complaint",input),/Refresh current assignment/);
  assert.equal(bodies[0],bodies[1]);assert.deepEqual(JSON.parse(bodies[0]),input);
 }finally{globalThis.fetch=originalFetch;if(oldWindow)Object.defineProperty(globalThis,"window",oldWindow);else Reflect.deleteProperty(globalThis,"window");}
});

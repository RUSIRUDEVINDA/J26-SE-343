import {test} from "node:test";import assert from "node:assert/strict";
import {NextRequest} from "next/server";
import {proxy} from "../src/proxy";
import {POST} from "../src/app/api/c4-session/route";
import {c4PostLoginPath,canShowGovernanceNavigation} from "../src/modules/component-04/services/postLogin";
import {c4NavigationFor} from "../src/modules/component-04/services/c4Navigation";
import {governanceCookie} from "../src/modules/component-04/services/governanceRouteAccess";
test("Commissioner landing and complaint navigation are distinct; applicant returns to its own dashboard",()=>{
 assert.equal(c4PostLoginPath({actorId:"0ab669fe-d0ab-5695-ad55-10f6b3b5aab3",institution:"COMMISSIONER"},"/"),"/component-04");
 assert.equal(c4NavigationFor(true).find(x=>x.label==="Complaint Inbox")?.href,"/component-04/central-complaints");assert.equal(c4NavigationFor(false).find(x=>x.label==="Complaint Inbox")?.href,"/component-04/complaint-inbox");
 assert.equal(c4PostLoginPath({actorId:"applicant",home:"/workflow-governance/applicant/cases"},"/"),"/workflow-governance/applicant/cases");
 assert.equal(canShowGovernanceNavigation({home:"/workflow-governance/applicant/cases"}),false);assert.equal(canShowGovernanceNavigation(null),false);assert.equal(canShowGovernanceNavigation({home:"/workflow-governance/cases"}),true);
});
test("Server proxy denies applicant every nested C4 route before rendering and never trusts forged headers",async()=>{
 const saved=globalThis.fetch;let calls=0;globalThis.fetch=async()=>{calls++;return Response.json({allowed:false,applicant:true},{status:403});};
 try{for(const path of ["/component-04","/component-04/assessments","/component-04/central-complaints","/component-04/officer-assessments/private/manual-evidence","/component-04/post-workflow/reports/private"]){const result=await proxy(new NextRequest("http://localhost:3000"+path,{headers:{cookie:`${governanceCookie}=test-token`,"x-c4-admitted":"true","x-c4-commissioner":"true"}}));assert.equal(result.status,307);assert.equal(result.headers.get("location"),"http://localhost:3000/workflow-governance/applicant/cases");assert.equal(result.headers.get("x-middleware-next"),null);}assert.equal(calls,5);}finally{globalThis.fetch=saved;}
});
test("Server route validates current bearer on every request and replaces browser-supplied identity headers",async()=>{
 const saved=globalThis.fetch;let allowed=true;globalThis.fetch=async(_url,init)=>{assert.equal(new Headers(init?.headers).get("authorization"),"Bearer test-token");assert.equal(init?.cache,"no-store");return Response.json(allowed?{allowed:true,isCommissioner:false}:{allowed:false,applicant:true},{status:allowed?200:403});};
 try{const request=()=>new NextRequest("http://localhost:3000/component-04/assessments",{headers:{cookie:`${governanceCookie}=test-token`,"x-c4-commissioner":"true"}});const ok=await proxy(request());assert.equal(ok.headers.get("x-middleware-request-x-c4-commissioner"),"false");allowed=false;assert.equal((await proxy(request())).status,307);}finally{globalThis.fetch=saved;}
});
test("Anonymous and revoked sessions fail closed; admission outage renders no protected children",async()=>{
 const saved=globalThis.fetch;try{assert.equal((await proxy(new NextRequest("http://localhost:3000/component-04"))).headers.get("location"),"http://localhost:3000/sign-in");globalThis.fetch=async()=>new Response(null,{status:401});const request=new NextRequest("http://localhost:3000/component-04",{headers:{cookie:`${governanceCookie}=revoked`}});assert.equal((await proxy(request)).headers.get("location"),"http://localhost:3000/sign-in");globalThis.fetch=async()=>{throw new Error("unavailable");};assert.equal((await proxy(request)).status,503);}finally{globalThis.fetch=saved;}
});
test("HttpOnly bridge requires same origin and server-valid token, preserving applicant deny classification",async()=>{
 const saved=globalThis.fetch;globalThis.fetch=async()=>Response.json({allowed:false,applicant:true},{status:403});
 try{const request=(origin:string)=>new NextRequest("http://localhost:3000/api/c4-session",{method:"POST",headers:{origin,authorization:"Bearer test-token"}});assert.equal((await POST(request("http://evil.example"))).status,403);const response=await POST(request("http://localhost:3000"));assert.equal(response.status,200);const cookie=response.headers.get("set-cookie")!;assert.match(cookie,/HttpOnly/i);assert.match(cookie,/SameSite=strict/i);assert.match(cookie,/Path=\/component-04/);globalThis.fetch=async()=>Response.json({},{status:401});assert.equal((await POST(request("http://localhost:3000"))).status,401);}finally{globalThis.fetch=saved;}
});

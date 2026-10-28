import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {c4NavigationFor} from "../src/modules/component-04/services/c4Navigation";
import {NextRequest} from "next/server";
import {proxy} from "../src/proxy";
import {governanceCookie} from "../src/modules/component-04/services/governanceRouteAccess";

const source=(path:string)=>readFileSync(`src/${path}`,"utf8");
test("Only currently admitted Commissioner/DS receive the uncached root redirect",async()=>{
 const original=globalThis.fetch;
 try{
  for(const isCommissioner of [true,false]){
   globalThis.fetch=async()=>Response.json({allowed:true,isCommissioner});
   for(const path of ["/component-04","/component-04/"]){
    const response=await proxy(new NextRequest("http://localhost:3000"+path,{headers:{cookie:`${governanceCookie}=valid-session`}}));
    assert.equal(response.status,307);
    assert.equal(response.headers.get("location"),"http://localhost:3000/component-04/assessments");
    assert.equal(response.headers.get("cache-control"),"private, no-store");
   }
  }
 }finally{globalThis.fetch=original;}
});
test("C4 navigation starts at Proposals without numbered icons and preserves complaint destinations",()=>{
 for(const commissioner of [true,false]){
  const items=c4NavigationFor(commissioner);
  assert.equal(items[0].href,"/component-04/assessments");
  assert.ok(items.every(item=>item.icon===""&&!/Overview|^\d/.test(item.label)));
  assert.equal(items.find(item=>item.label==="Complaint Inbox")?.href,commissioner?"/component-04/central-complaints":"/component-04/complaint-inbox");
  assert.equal(new Set(items.map(item=>item.href)).size,items.length);
 }
});
test("Default C4 entry redirects inside the guarded layout without the tools disclosure",()=>{
 assert.match(source("app/component-04/page.tsx"),/redirect\("\/component-04\/assessments"\)/);
 const layout=source("app/component-04/layout.tsx");
 assert.doesNotMatch(layout,/More governance tools and evidence/);
 assert.match(layout,/GovernanceSessionBoundary/);
 assert.match(layout,/x-c4-admitted/);
 const proxy=source("proxy.ts");
 assert.ok(proxy.indexOf('if(!response.ok||!body.allowed)')<proxy.indexOf('if(request.nextUrl.pathname==='));
 assert.match(proxy,/NextResponse.redirect\(new URL\("\/component-04\/assessments",request.url\)\)/);
});
test("Shared account header keeps signout and expiry messages without the redundant signed-in banner",()=>{
 assert.match(source("shared/components/PageLayout.tsx"),/<AppHeader unifiedAccount/);
 const session=source("shared/components/DevelopmentSessionBar.tsx");
 assert.doesNotMatch(session,/<strong>Signed in<\/strong>|#fff6dc/);
 assert.match(session,/signOutDevelopment/);
 assert.match(session,/Session expired\. Sign in again\./);
 assert.match(session,/role="status"/);
});
test("Applicant complaint sign-in retains its allowed return destination and readable button class",()=>{
 const complaints=source("modules/component-04/components/RegisteredComplaints.tsx");
 assert.match(complaints,/className=\{inboxStyles.primary\} href="\/sign-in\?returnTo=%2Fworkflow-governance%2Fapplicant%2Fcomplaints"/);
 assert.match(source("modules/component-04/components/RegisteredComplaints.module.css"),/\.primary,\.primary:visited\{[^}]*color:#fff!important/);
});
test("Named workflow labels and saved-status layout avoid the reported punctuation and wrapping defects",()=>{
 for(const file of ["OfficerAnomalyPanel.tsx","OfficerSavedReports.tsx"]){
  const text=source("modules/component-04/components/"+file);
  assert.doesNotMatch(text,/\? Named route v2/);
  assert.match(text,/— Named route v2/);
 }
 assert.match(source("modules/component-04/components/ProgressiveSteps.module.css"),/\.badge\{white-space:nowrap/);
 assert.match(source("modules/component-04/components/KiwulaAssessmentWorkspace.tsx"),/className=\{styles.proposalActions\}/);
 for(const file of ["EarlyGovernanceInvestigation.tsx","OfficerAnomalyPanel.tsx","RegisteredComplaints.tsx"])
  assert.doesNotMatch(source("modules/component-04/components/"+file),/<PendingIndicator>[^<]*\?<\/PendingIndicator>/);
});

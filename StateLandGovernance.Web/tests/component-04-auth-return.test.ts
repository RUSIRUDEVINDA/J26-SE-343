import {test} from "node:test";
import assert from "node:assert/strict";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {readFileSync} from "node:fs";
import {SignInDestinationLink} from "../src/shared/components/SignInLink";
import {applicantComplaintReturn,applicantComplaintSignIn} from "../src/modules/component-04/services/applicantComplaintReturn";
import {c4PostLoginPath} from "../src/modules/component-04/services/postLogin";
import {signInToPlatform,readDevelopmentSession} from "../src/lib/developmentSession";
const complaints="/workflow-governance/applicant/complaints";
test("Header and page complaint sign-in entries carry the identical exact return destination",()=>{
 const header=renderToStaticMarkup(createElement(SignInDestinationLink,{pathname:complaints,className:"signIn"}));
 const href=applicantComplaintSignIn(complaints);
 assert.match(header,/href="\/sign-in\?returnTo=%2Fworkflow-governance%2Fapplicant%2Fcomplaints"/);
 const page=readFileSync("src/modules/component-04/components/RegisteredComplaints.tsx","utf8");
 assert.ok(page.includes(`href="${href}"`));
 const destination=new URL(href,"http://localhost:3000").searchParams.get("returnTo");
 assert.equal(applicantComplaintReturn({home:"/workflow-governance/applicant/cases"},destination),complaints);
});
test("Shared header does not turn other pages into a complaint return",()=>{
 for(const path of [null,undefined,"/","/component-04","/workflow-governance/applicant/cases","//evil.test",complaints+"/",complaints+"?x=1"])
  assert.equal(applicantComplaintSignIn(path),"/sign-in");
 for(const file of ["AppHeader.tsx","DevelopmentSessionBar.tsx"])
  assert.match(readFileSync("src/shared/components/"+file,"utf8"),/<SignInLink className=\{styles.signIn\}/);
});
test("Failed login keeps the current return URL and non-applicants retain their existing routing",()=>{
 const page=readFileSync("src/app/sign-in/page.tsx","utf8");
 assert.match(page,/new URLSearchParams\(window.location.search\).get\("returnTo"\)/);
 assert.match(page,/catch\(e\)\{setError/);
 assert.doesNotMatch(page,/history.replaceState|searchParams.delete/);
 const commissioner={actorId:"0ab669fe-d0ab-5695-ad55-10f6b3b5aab3",institution:"COMMISSIONER",home:"/"};
 assert.equal(applicantComplaintReturn(commissioner,complaints),null);
 assert.equal(c4PostLoginPath(commissioner,"/"),"/component-04");
 assert.equal(c4PostLoginPath({actorId:"ds",home:"/workflow-governance/cases"},"/"),"/");
 assert.equal(c4PostLoginPath({actorId:"applicant",home:"/workflow-governance/applicant/cases"},"/"),"/workflow-governance/applicant/cases");
});
test("External, encoded, malformed and non-allowlisted return URLs remain rejected",()=>{
 const user={home:"/workflow-governance/applicant/cases"};
 for(const value of ["https://evil.test", "//evil.test", "/\\evil.test", "%2Fworkflow-governance%2Fapplicant%2Fcomplaints", complaints+"#x",complaints+"?x=1","/component-04","javascript:alert(1)",complaints+"\n",complaints+"%00"])
  assert.equal(applicantComplaintReturn(user,value),null);
});
test("Successful applicant authentication retains the session across complaint view refresh",async()=>{
 const originalFetch=globalThis.fetch,originalWindow=Object.getOwnPropertyDescriptor(globalThis,"window");
 const values=new Map<string,string>();
 Object.defineProperty(globalThis,"window",{configurable:true,value:{sessionStorage:{getItem:(key:string)=>values.get(key)??null,setItem:(key:string,value:string)=>values.set(key,value),removeItem:(key:string)=>values.delete(key)},dispatchEvent:()=>true}});
 try{
  globalThis.fetch=async()=>Response.json({accessToken:"isolated-test-token",expiresAtUtc:new Date(Date.now()+60000).toISOString(),user:{actorId:"fictional-applicant",home:"/workflow-governance/applicant/cases",role:"Applicant"}});
  let fallback="unchanged";
  const session=await signInToPlatform("isolated-user","isolated-password",value=>{fallback=value;});
  assert.equal(fallback,"/");
  const returnTo=new URL(applicantComplaintSignIn(complaints),"http://localhost:3000").searchParams.get("returnTo");
  assert.equal(applicantComplaintReturn(session.user,returnTo),complaints);
  assert.equal(readDevelopmentSession()?.user.actorId,"fictional-applicant");
  assert.equal(readDevelopmentSession()?.accessToken,"isolated-test-token");
  values.clear();globalThis.fetch=async()=>new Response(null,{status:401});fallback="unchanged";
  await assert.rejects(signInToPlatform("isolated-user","wrong",value=>{fallback=value;}),/Incorrect username or password/);
  assert.equal(fallback,"unchanged");assert.equal(readDevelopmentSession(),null);
  assert.equal(returnTo,complaints);
 }finally{globalThis.fetch=originalFetch;if(originalWindow)Object.defineProperty(globalThis,"window",originalWindow);else Reflect.deleteProperty(globalThis,"window");}
});

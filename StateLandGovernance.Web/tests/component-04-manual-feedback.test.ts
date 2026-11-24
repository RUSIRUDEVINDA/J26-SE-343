import {test} from "node:test";
import assert from "node:assert/strict";
import {createHash} from "node:crypto";
import {createElement} from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {readFileSync} from "node:fs";
import {EvidenceUpload,validateEvidenceFile,type UploadState} from "../src/modules/component-04/services/evidenceUpload";
import {C4RequestError,confirmedJson} from "../src/modules/component-04/services/requestOutcome";
import {UploadFeedback} from "../src/modules/component-04/components/ManualEvidenceVerification";
import {orderedHandlingHistory,type HandlingView} from "../src/modules/component-04/services/complaintHandling";
import {type EvidenceAttachment,type EvidenceView} from "../src/modules/component-04/services/manualEvidence";
import {applicantComplaintReturn,applicantComplaintSignIn} from "../src/modules/component-04/services/applicantComplaintReturn";
import {signInToPlatform,signOutDevelopment,readDevelopmentSession} from "../src/lib/developmentSession";
const pdf=()=>new File(["%PDF-1.4\nFictional isolated UI fixture\n%%EOF"],"Proposal1.pdf",{type:"application/pdf"});
function fixture(){
 const states:UploadState[]=[],rows:EvidenceAttachment[]=[],forms:FormData[]=[],readbacks:EvidenceView[]=[];let keys=0;
 const deps={key:()=>`key-${++keys}`,changed:(state:UploadState)=>states.push(state),updated:(view:EvidenceView)=>readbacks.push(view),read:async()=>({canVerify:true,attachments:[...rows],history:[]}),attach:async(form:FormData,waiting:()=>void)=>{
  forms.push(form);waiting();const file=form.get("file") as File;
  const row={id:"attachment-1",requestId:"request-1",concern:String(form.get("concern")),actorId:"fictional-ds",createdAtUtc:"2026-10-23T00:00:00Z",fileName:file.name,contentType:file.type,bytes:file.size,sha256:createHash("sha256").update(Buffer.from(await file.arrayBuffer())).digest("hex")};rows.push(row);return row;
 }};
 return {deps,states,rows,forms,readbacks,keys:()=>keys};
}
test("PDF upload announces real phases and only server-confirmed saved metadata enters the list",async()=>{
 const f=fixture(),u=new EvidenceUpload(f.deps);u.select(pdf(),"LegalDispute");assert.equal(u.state.phase,"ready");assert.equal(f.rows.length,0);
 await Promise.all([u.start("LegalDispute"),u.start("LegalDispute")]);
 assert.equal(f.forms.length,1);assert.equal(f.keys(),1);
 assert.deepEqual(f.states.map(x=>x.phase),["ready","validating","uploading","confirming","saved","saved"]);
 assert.equal(u.state.phase,"saved");assert.match(u.state.message,/Upload successful/);assert.equal(f.readbacks.at(-1)?.attachments.length,1);
 const reopened=new EvidenceUpload(f.deps);await reopened.refresh();assert.equal(reopened.state.phase,"loaded");
 assert.equal(f.readbacks.at(-1)?.attachments[0].sha256,f.rows[0].sha256);
});
test("Accepted upload followed by failed refresh remains saved and cannot replay a second write",async()=>{
 const f=fixture();let fail=true;const read=f.deps.read;f.deps.read=async()=>{if(fail)throw new Error("offline");return read();};
 const u=new EvidenceUpload(f.deps);u.select(pdf(),"LegalDispute");await u.start("LegalDispute");
 assert.equal(u.state.phase,"saved");assert.match(u.state.message,/Upload accepted; attachment list could not be refreshed/);assert.equal(u.canReplay,false);
 await u.replay();assert.equal(f.forms.length,1);fail=false;await u.refresh();assert.match(u.state.message,/Upload successful/);
});
test("Lost response retains identical key/file/concern; refresh never writes and replay creates no duplicate",async()=>{
 const f=fixture(),original=f.deps.attach;let first=true;f.deps.attach=async(form,waiting)=>{if(first){first=false;await original(form,waiting);throw new C4RequestError("lost","uncertain");}f.forms.push(form);waiting();return f.rows[0];};
 const u=new EvidenceUpload(f.deps);u.select(pdf(),"LegalDispute");await u.start("LegalDispute");assert.equal(u.state.phase,"uncertain");
 u.select(new File(["different"],"other.txt",{type:"text/plain"}),"Other");await u.start("Other");await u.refresh();assert.equal(f.forms.length,1);assert.equal(u.state.phase,"uncertain");
 await u.replay();assert.equal(f.forms[0],f.forms[1]);assert.equal(f.rows.length,1);assert.equal(f.keys(),1);assert.equal(u.state.phase,"saved");
});
test("Definite rejection permits deliberate retry with selected file while no-file-after-reload cannot replay",async()=>{
 const f=fixture();f.deps.attach=async()=>{throw new C4RequestError("Permission denied","rejected",403);};
 const u=new EvidenceUpload(f.deps);u.select(pdf(),"LegalDispute");await u.start("LegalDispute");assert.equal(u.state.phase,"failed");assert.equal(u.canReplay,false);assert.match(u.state.message,/Permission denied/);
 const restored=new EvidenceUpload(f.deps,{submissionKey:"original",fileName:"Proposal1.pdf",bytes:20,concern:"LegalDispute"});await restored.start("LegalDispute");await restored.replay();assert.equal(restored.state.phase,"uncertain");assert.equal(restored.canReplay,false);assert.match(restored.state.message,/original file is no longer available/);
});
test("Client validation rejects oversize, empty, unsupported and mismatched files without a request",async()=>{
 for(const file of [new File([new Uint8Array(5*1024*1024+1)],"large.pdf",{type:"application/pdf"}),new File([],"empty.pdf",{type:"application/pdf"}),new File(["no"],"wrong.pdf",{type:"application/pdf"}),new File(["no"],"app.exe",{type:"application/octet-stream"})])await assert.rejects(validateEvidenceFile(file));
 const f=fixture(),u=new EvidenceUpload(f.deps);u.select(new File(["not a pdf"],"wrong.pdf",{type:"application/pdf"}),"LegalDispute");await u.start("LegalDispute");assert.equal(f.forms.length,0);assert.equal(f.keys(),0);assert.equal(u.state.phase,"failed");
});
test("Error transport reports real validation messages and distinguishes lost acknowledgement",async()=>{
 await assert.rejects(confirmedJson(new Response(JSON.stringify({message:"File content does not match declared type"}),{status:400}),true),(e:unknown)=>e instanceof C4RequestError&&e.outcome==="rejected"&&e.message.includes("declared type"));
 for(const response of [new Response("unavailable",{status:503}),new Response("broken json",{status:200})])await assert.rejects(confirmedJson(response,true),(e:unknown)=>e instanceof C4RequestError&&e.outcome==="uncertain");
});
test("Upload status is an accessible indeterminate announcement without fake percentages",()=>{
 const html=renderToStaticMarkup(createElement(UploadFeedback,{state:{phase:"uploading",fileName:"Proposal1.pdf",bytes:42,concern:"LegalDispute",message:"Uploading Proposal1.pdf…"}}));
 assert.match(html,/role="status"/);assert.match(html,/aria-live="polite"/);assert.match(html,/Uploading Proposal1.pdf/);assert.doesNotMatch(html,/aria-valuenow|\d+%/);
 const text=readFileSync("src/modules/component-04/components/ManualEvidenceVerification.tsx","utf8");assert.match(text,/pendingDecision/);assert.doesNotMatch(text,/pending.current.form/);assert.match(text,/This is not an evidence upload/);assert.match(text,/<h4>Evidence attachments/);assert.match(text,/view.canVerify&&<fieldset/);
});
test("Handling deduplicates only identical event IDs, preserves repeated triage writes and never guesses hidden states",()=>{
 const event={id:"a",revision:2,operation:"DsReviewRecorded",status:"AssignedToDs",atUtc:"2026-10-23T00:00:00Z",publicSummary:"",actorId:null,reason:null,details:null,evidenceReferences:null};
 const view={history:[event,{...event,id:"b",revision:3},event]} as HandlingView;const rows=orderedHandlingHistory(view);assert.equal(rows.length,2);assert.equal(rows[0].fromStatus,null);assert.equal(rows[1].fromStatus,"AssignedToDs");
 assert.equal(orderedHandlingHistory({history:[{...event,revision:1,operation:"StartInvestigation",status:"UnderInvestigation"}]} as HandlingView)[0].fromStatus,null);
 const panel=readFileSync("src/modules/component-04/components/ComplaintHandlingPanel.tsx","utf8");assert.match(panel,/recorded successfully/);assert.match(panel,/setHistoryOpen\(true\)/);assert.match(panel,/pending.current\?\?/);assert.match(panel,/definitelyRejected/);
 for(const file of ["RegisteredComplaints.tsx","CentralComplaints.tsx"]){const source=readFileSync("src/modules/component-04/components/"+file,"utf8");assert.doesNotMatch(source,/key=\{`\$\{[^}]+\}:\$\{[^}]+state.revision/);assert.match(source,/retain=false/);}
});
test("Repeated applicant signout/signin preserves complaint return while plain and officer routing stay unchanged",async()=>{
 const oldFetch=globalThis.fetch,oldWindow=Object.getOwnPropertyDescriptor(globalThis,"window"),values=new Map<string,string>();
 Object.defineProperty(globalThis,"window",{configurable:true,value:{sessionStorage:{getItem:(k:string)=>values.get(k)??null,setItem:(k:string,v:string)=>values.set(k,v),removeItem:(k:string)=>values.delete(k)},dispatchEvent:()=>true}});
 try{globalThis.fetch=async url=>String(url).endsWith("sign-out")?new Response(null,{status:204}):Response.json({accessToken:"isolated-token",expiresAtUtc:new Date(Date.now()+60000).toISOString(),user:{actorId:"isolated-applicant",home:"/workflow-governance/applicant/cases"}});
  const path="/workflow-governance/applicant/complaints";
  for(let i=0;i<3;i++){const link=applicantComplaintSignIn(path),query=new URL(link,"http://localhost:3000").searchParams.get("returnTo");assert.equal(applicantComplaintSignIn("/sign-in",query),link);const session=await signInToPlatform("fixture","fixture",()=>{});assert.equal(applicantComplaintReturn(session.user,query),path);assert.ok(readDevelopmentSession());await signOutDevelopment();assert.equal(readDevelopmentSession(),null);assert.equal(applicantComplaintSignIn(path),"/sign-in?returnTo=%2Fworkflow-governance%2Fapplicant%2Fcomplaints");}
  for(const bad of ["https://evil.test","//evil.test","/component-04",path+"?x=1"])
   assert.equal(applicantComplaintSignIn("/sign-in",bad),"/sign-in");
  assert.equal(applicantComplaintSignIn("/component-04"),"/sign-in");assert.equal(applicantComplaintReturn({home:"/component-04"},path),null);
  assert.match(readFileSync("src/shared/components/DevelopmentSessionBar.tsx","utf8"),/router.push\(applicantComplaintSignIn\(pathname\)\)/);
 }finally{globalThis.fetch=oldFetch;if(oldWindow)Object.defineProperty(globalThis,"window",oldWindow);else Reflect.deleteProperty(globalThis,"window");}
});

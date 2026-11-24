import {type EvidenceAttachment,type EvidenceView} from "./manualEvidence";
import {C4RequestError,definitelyRejected} from "./requestOutcome";
export type UploadPhase="empty"|"ready"|"validating"|"uploading"|"confirming"|"saved"|"failed"|"uncertain"|"loaded";
export type UploadState={phase:UploadPhase;message:string;fileName?:string;bytes?:number;concern?:string;accepted?:EvidenceAttachment;refreshFailed?:boolean;refreshing?:boolean;canReplay?:boolean};
type Marker={submissionKey:string;fileName:string;bytes:number;concern:string};
export type UploadDependencies={requestId?:string;attach:(form:FormData,waiting:()=>void)=>Promise<EvidenceAttachment>;read:()=>Promise<EvidenceView>;updated:(view:EvidenceView)=>void;changed:(state:UploadState)=>void;key:()=>string;marker?:(value:Marker|null)=>void};
export async function validateEvidenceFile(file:File){
 if(file.size>5*1024*1024)throw new C4RequestError("File exceeds 5 MiB.","rejected");
 if(file.size===0)throw new C4RequestError("Select a nonempty evidence file.","rejected");
 const bytes=new Uint8Array(await file.arrayBuffer());let valid=false;
 if(file.type==="application/pdf")valid=new TextDecoder().decode(bytes.slice(0,5))==="%PDF-";
 else if(file.type==="image/png")valid=[137,80,78,71,13,10,26,10].every((v,i)=>bytes[i]===v);
 else if(file.type==="image/jpeg")valid=bytes[0]===255&&bytes[1]===216&&bytes[2]===255;
 else if(file.type==="text/plain"){try{valid=!/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/.test(new TextDecoder("utf-8",{fatal:true}).decode(bytes));}catch{}}
 else throw new C4RequestError("Unsupported file type. Use PDF, PNG, JPEG or UTF-8 text.","rejected");
 if(!valid)throw new C4RequestError("File content does not match the declared type.","rejected");
}
// One exact multipart payload is retained only in memory; no automatic write/retry.
export class EvidenceUpload{
 state:UploadState={phase:"empty",message:"No file selected."};private file:File|null=null;private pending:FormData|null=null;private active=true;private flight=false;
 private deps:UploadDependencies;
 constructor(deps:UploadDependencies,previous?:Marker|null){this.deps=deps;if(previous&&typeof previous.fileName==="string"&&typeof previous.concern==="string"&&typeof previous.submissionKey==="string"&&Number.isFinite(previous.bytes))this.state={phase:"uncertain",fileName:previous.fileName,bytes:previous.bytes,concern:previous.concern,message:"Upload status unconfirmed. Check saved attachments before retrying. The original file is no longer available for exact replay after reload; no replacement request will be created."};}
 get busy(){return this.flight;}get canReplay(){return this.pending!==null;}get unresolved(){return this.state.phase==="uncertain";}
 private set(state:UploadState){if(this.active){this.state={...state,canReplay:this.pending!==null};this.deps.changed(this.state);}}
 dispose(){this.active=false;this.file=null;this.pending=null;}
 select(file:File|null,concern:string){if(this.flight||this.unresolved)return;this.file=file;this.set(file?{phase:"ready",fileName:file.name,bytes:file.size,concern,message:`${file.name} selected and ready. Not uploaded yet.`}:{phase:"empty",message:"No file selected."});}
 loaded(view:EvidenceView){if(this.state.phase==="empty"&&view.attachments.length)this.set({phase:"loaded",message:`${view.attachments.length} previously saved attachment(s) loaded from the server.`});}
 async refresh(){const view=await this.deps.read();if(!this.active)return;this.deps.updated(view);this.loaded(view);if(this.state.accepted){const found=view.attachments.some(a=>a.id===this.state.accepted!.id);this.set({...this.state,refreshing:false,refreshFailed:!found,message:found?`Upload successful — ${this.state.accepted.fileName} saved`:`Upload accepted; attachment list could not be refreshed. Refresh attachments without submitting another write.`});}}
 async start(concern:string){
  if(this.flight||this.unresolved)return;
  if(!this.file){this.set({phase:"failed",message:"Select an evidence file."});return;}
  this.flight=true;const file=this.file;this.set({phase:"validating",fileName:file.name,bytes:file.size,concern,message:`Validating ${file.name}…`});
  try{await validateEvidenceFile(file);if(!this.active)return;const form=new FormData();const key=this.deps.key();form.set("submissionKey",key);form.set("concern",concern);form.set("file",file);this.pending=form;this.deps.marker?.({submissionKey:key,fileName:file.name,bytes:file.size,concern});await this.send();}
  catch(error){this.failure(error);}finally{this.flight=false;}
 }
 async replay(){if(this.flight||!this.pending||!this.unresolved)return;this.flight=true;try{await this.send();}catch(error){this.failure(error);}finally{this.flight=false;}}
 private failure(error:unknown){if(!this.active)return;const rejected=definitelyRejected(error)||!this.pending;if(rejected){this.pending=null;this.deps.marker?.(null);}this.set({...this.state,phase:rejected?"failed":"uncertain",message:rejected?(error instanceof Error?error.message:"Upload rejected."):"Upload status unconfirmed. Check saved attachments before retrying. The exact key and file are retained for deliberate replay; no new request is created."});}
 private async send(){
  this.set({...this.state,phase:"uploading",message:`Uploading ${this.state.fileName}…`});
  const saved=await this.deps.attach(this.pending!,()=>this.set({...this.state,phase:"confirming",message:`Waiting for server confirmation — ${this.state.fileName}…`}));
  if(!this.active)return;
  if(!saved.id||this.deps.requestId&&saved.requestId!==this.deps.requestId||saved.concern!==this.state.concern||saved.fileName!==this.state.fileName?.trim()||saved.bytes!==this.state.bytes)throw new C4RequestError("Attachment response did not match the submitted context.","uncertain");
  this.pending=null;this.file=null;this.deps.marker?.(null);this.set({...this.state,phase:"saved",accepted:saved,refreshing:true,message:`Upload successful — ${saved.fileName} saved. Refreshing attachment list…`});
  try{await this.refresh();}catch{this.set({...this.state,refreshing:false,refreshFailed:true,message:"Upload accepted; attachment list could not be refreshed. Refresh attachments without submitting another write."});}
 }
}

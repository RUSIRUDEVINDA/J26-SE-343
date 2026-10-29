import type {Finding} from "./officerFlows";
import type {DraftInvestigation,InvestigationFile} from "./investigationEvidence";
export const pendingDraftKey=(actor:string)=>`c4-pending-draft-save/1/${actor}`;
export const pendingFilesKey=(actor:string)=>`c4-pending-draft-files/1/${actor}`;
export type PendingFile={key:string;concern:string;name:string;type:string;size:number;sha256:string;file?:File};
export type DraftSaveBatch={submissionKey:string;proposalId:string;supersedesId:string|null;findings:Finding[];investigationId:string|null;uploads:PendingFile[]};
export function fileDescriptor(p:PendingFile):PendingFile{return {key:p.key,concern:p.concern,name:p.name,type:p.type,size:p.size,sha256:p.sha256};}
export async function pendingFile(concern:string,file:File,key=crypto.randomUUID()):Promise<PendingFile>{
 if(file.size<1||file.size>5*1024*1024)throw new Error("Evidence must be between 1 byte and 5 MiB.");
 if(!file.name.trim()||file.name.trim().length>150||/[\x00-\x1f\x7f/\\:]/.test(file.name))throw new Error("Use a safe evidence filename (up to 150 characters).");
 const bytes=new Uint8Array(await file.arrayBuffer());let valid=false;
 if(file.type==="application/pdf")valid=new TextDecoder().decode(bytes.slice(0,5))==="%PDF-";
 if(file.type==="image/png")valid=[137,80,78,71,13,10,26,10].every((n,i)=>bytes[i]===n);
 if(file.type==="image/jpeg")valid=bytes[0]===255&&bytes[1]===216&&bytes[2]===255;
 if(file.type==="text/plain")try{valid=!/[\x00-\x08\x0b\x0c\x0e-\x1f\x7f-\x9f]/.test(new TextDecoder("utf-8",{fatal:true}).decode(bytes));}catch{}
 if(!valid)throw new Error("Choose a PDF, PNG, JPEG or UTF-8 text file whose content matches its type.");
 const sha256=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",bytes)),n=>n.toString(16).padStart(2,"0")).join("");
 return {key,concern,name:file.name.trim(),type:file.type,size:file.size,sha256,file};
}
export function samePendingFile(a:PendingFile,b:PendingFile){return a.concern===b.concern&&a.name===b.name&&a.type===b.type&&a.size===b.size&&a.sha256===b.sha256;}
export function persistedFile(p:PendingFile,a:InvestigationFile){return a.concern===p.concern&&a.fileName===p.name&&a.contentType===p.type&&a.bytes===p.size&&a.sha256===p.sha256;}
export type DraftSaveApi={create:(batch:DraftSaveBatch)=>Promise<DraftInvestigation>;read:(id:string)=>Promise<{attachments:InvestigationFile[]}>;attach:(id:string,p:PendingFile)=>Promise<InvestigationFile>};
// Frozen submission keys and payload survive uncertain responses. Every retry reconciles saved files first.
export async function saveDraftWithFiles(batch:DraftSaveBatch,files:PendingFile[],api:DraftSaveApi,checkpoint:(batch:DraftSaveBatch)=>void):Promise<string>{
 if(!batch.investigationId){const saved=await api.create(batch);batch={...batch,investigationId:saved.id};checkpoint(batch);}
 const id=batch.investigationId!;let saved=(await api.read(id)).attachments;
 for(const upload of batch.uploads){
  if(saved.some(a=>persistedFile(upload,a)))continue;
  const available=files.find(f=>samePendingFile(f,upload));
  if(!available?.file)throw new Error(`Reselect ${upload.name} for ${upload.concern}. Browser file contents cannot survive a reload; the saved draft and submission keys are retained.`);
  const actual=await pendingFile(upload.concern,available.file,upload.key);if(!samePendingFile(actual,upload))throw new Error(`The reselected file differs from ${upload.name}. Reselect the original file to safely resume this save.`);
  const metadata=await api.attach(id,{...upload,file:available.file});
  if(metadata.investigationId!==id||!persistedFile(upload,metadata))throw new Error("The returned attachment identity could not be confirmed. Refresh and retry the same save.");
  saved=[...saved,metadata];
 }
 const confirmed=(await api.read(id)).attachments;
 if(batch.uploads.some(p=>!confirmed.some(a=>persistedFile(p,a))))throw new Error("Draft saved, but not all selected attachments are confirmed. Retry this same save.");
 return id;
}

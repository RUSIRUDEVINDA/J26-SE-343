"use client";
import { useEffect, useState } from "react";
import { apiDownloadBlob } from "@/lib/apiClient";
import { workflowAuthRole, workflowDevelopmentHeaders } from "../services/workflowDevelopmentHeaders";
export function ProposalEvidenceViewer({caseId, documentId, versionId, pageNumber}: {caseId:string;documentId?:string;versionId?:string;pageNumber?:number|null}) {
  const [url,setUrl]=useState<string>(); const [error,setError]=useState(""); const [open,setOpen]=useState(false);
  useEffect(()=>{setOpen(false);},[caseId,documentId,versionId]);
  useEffect(()=>{let active=true;let objectUrl:string|undefined;setUrl(undefined);setError("");
    if(open&&documentId&&versionId) apiDownloadBlob(`/api/workflow-governance/cases/${caseId}/documents/${documentId}/versions/${versionId}/download`,workflowAuthRole("officer"),workflowDevelopmentHeaders("officer"))
      .then(blob=>{if(active){objectUrl=URL.createObjectURL(!blob.type || blob.type === "application/octet-stream" ? new Blob([blob],{type:"application/pdf"}) : blob);setUrl(objectUrl);}})
      .catch(e=>{if(active)setError(e instanceof Error?e.message:"Source unavailable");});
    return()=>{active=false;if(objectUrl)URL.revokeObjectURL(objectUrl);};
  },[caseId,documentId,versionId,open]);
  return <aside aria-label="Selected Proposal evidence"><h4>Exact selected Proposal source</h4>
    {!documentId||!versionId?<p>Select a Proposal version first.</p>:!open?<button type="button" onClick={()=>setOpen(true)}>Open PDF beside review</button>:<>
      <button type="button" onClick={()=>setOpen(false)}>Close PDF</button>{error?<p role="alert">{error}</p>:url?<><a href={url} target="_blank" rel="noopener noreferrer">Open exact PDF in separate tab</a><iframe title="Selected Proposal PDF" src={`${url}#page=${pageNumber||1}`} style={{width:"100%",height:"65vh",border:"1px solid #ccd5df"}}/></>:<p>Loading authorized document...</p>}</>}
  </aside>;
}

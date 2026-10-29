"use client";
import {useEffect,useState} from "react";
import {EarlyGovernanceStepOne} from "./EarlyGovernanceStepOne";
import {useOfficerSession} from "./OfficerFlowSession";
import {PendingIndicator} from "./PendingIndicator";
import {officerApi,type RequestReport} from "../services/officerFlows";
export function EarlyGovernanceInvestigation(props:{onResult?:(report:RequestReport|null)=>void}){
 const {actor,epoch,generation}=useOfficerSession();const [capability,setCapability]=useState<{actor:string;version:boolean}|null>(null);
 useEffect(()=>{let active=true;const version=epoch.current;if(actor)void officerApi<{version:string}>("officer-investigation-evidence-capabilities").then(c=>{if(active&&version===epoch.current)setCapability({actor,version:c.version==="investigation-evidence/1"});}).catch(()=>{if(active&&version===epoch.current)setCapability({actor,version:false});});return()=>{active=false;};},[actor,epoch,generation]);
 if(!actor||capability?.actor!==actor)return <PendingIndicator>Checking current investigation capabilities…</PendingIndicator>;
 return capability.version?<EarlyGovernanceStepOne {...props}/>:<p role="alert">The current finalized-investigation workflow is unavailable from this backend. No historical workflow is substituted.</p>;
}

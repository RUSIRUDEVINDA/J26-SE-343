"use client";
import {useEffect,useState} from "react";
import Link from "next/link";
import {apiGet} from "@/lib/apiClient";
import {PageLayout,PageHeading} from "@/shared/components/PageLayout";
import {workflowGovernanceNavItems,WORKFLOW_GOVERNANCE_BASE} from "@/shared/constants/navigation";
type Item={caseId:string;applicationReference:string;status:string;responsibleOffice?:string;assignmentStatus:string};
export default function MyApplications(){const[items,setItems]=useState<Item[]>([]),[error,setError]=useState(""),[loaded,setLoaded]=useState(false);useEffect(()=>{void apiGet<Item[]>("/api/workflow-governance/intake-assignments/applicant").then(setItems).catch(e=>setError(e.message)).finally(()=>setLoaded(true));},[]);return <PageLayout navigationItems={workflowGovernanceNavItems} navigationBasePath={WORKFLOW_GOVERNANCE_BASE}><PageHeading title="My applications" subtitle="Your persisted submissions and requested corrections"/><p><Link href="/workflow-governance/applicant/intake">Submit a proposal</Link></p>{error&&<p role="alert">{error}</p>}{loaded&&!error&&items.length===0&&<p>No submitted applications.</p>}<ul>{items.map(x=><li key={x.caseId}><Link href={"/workflow-governance/applicant/tracking/"+encodeURIComponent(x.applicationReference)}>{x.applicationReference}</Link> ? {x.status} ? {x.assignmentStatus}{x.responsibleOffice&&" ? "+x.responsibleOffice}</li>)}</ul></PageLayout>}

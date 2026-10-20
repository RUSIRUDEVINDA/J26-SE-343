"use client";
import {useEffect,useState} from "react";
import Link from "next/link";
import {apiGet} from "@/lib/apiClient";
import {readDevelopmentSession} from "@/lib/developmentSession";
import {PageLayout} from "@/shared/components/PageLayout";
type Requests={institution:string;executionStatus:string;requests:{stageId:string;code:string;status:string;href:string}[]};
export default function MyRequests(){
 const [state,setState]=useState<Requests>();const [error,setError]=useState("");const [commissioner,setCommissioner]=useState(false);const [rejections,setRejections]=useState<string[]>();
 useEffect(()=>{const session=readDevelopmentSession();setCommissioner(session?.user.institution==="COMMISSIONER");void apiGet<Requests>("/api/workflow-governance/cases/assigned-institutional-requests",undefined,"officer").then(setState).catch(e=>setError(e.message));
 if(session?.user.institution==="COMMISSIONER")void apiGet<string[]>("/api/workflow-governance/cases/workflow-execution/rejected",undefined,"officer").then(setRejections).catch(e=>setError(e.message));},[]);
 return <PageLayout showSidebar={false}><h1>My assigned institutional requests</h1>{error&&<p role="alert">{error}</p>}{state&&<><p>Institution: {state.institution}</p>{state.requests.length===0?<p>No activated institutional requests are assigned yet. Fact review, completeness, screening and an approved plan must precede execution.</p>:<ul>{state.requests.map(r=><li key={r.stageId}><Link href={r.href}>{r.code}: {r.status}</Link></li>)}</ul>}</>}
 {commissioner&&<section><h2>Assigned rejection reports</h2>{rejections?.length===0?<p>No rejected cases in your assignment.</p>:rejections?.map(id=><p key={id}>A rejected execution is recorded for your assigned case. Review its restricted Commissioner request when available.</p>)}</section>}</PageLayout>;
}

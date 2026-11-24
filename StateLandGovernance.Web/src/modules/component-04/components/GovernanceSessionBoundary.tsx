"use client";
import {useEffect,useState,type ReactNode} from "react";
import {useRouter} from "next/navigation";
import {readDevelopmentSession} from "@/lib/developmentSession";
// Account-switch privacy in addition to the mandatory server proxy and API policies.
export function GovernanceSessionBoundary({children}:{children:ReactNode}){
 const router=useRouter();const [allowed,setAllowed]=useState(false);
 useEffect(()=>{const update=()=>{const user=readDevelopmentSession()?.user;const applicant=user?.home?.startsWith("/workflow-governance/applicant/");setAllowed(!!user&&!applicant);if(applicant)router.replace("/workflow-governance/applicant/cases");else if(!user)router.replace("/sign-in");};update();window.addEventListener("development-session-changed",update);return()=>window.removeEventListener("development-session-changed",update);},[router]);
 return allowed?<>{children}</>:<p role="status">Checking governance access…</p>;
}

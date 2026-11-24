"use client";
import Link from "next/link";
import {useEffect,useState} from "react";
import {readDevelopmentSession} from "@/lib/developmentSession";
import {canShowGovernanceNavigation} from "../services/postLogin";
export function GovernanceModuleLink(){const [visible,setVisible]=useState(false);useEffect(()=>{const refresh=()=>setVisible(canShowGovernanceNavigation(readDevelopmentSession()?.user));refresh();window.addEventListener("development-session-changed",refresh);return()=>window.removeEventListener("development-session-changed",refresh);},[]);return visible?<Link href="/component-04" className="landingCard"><h2>Governance Intelligence</h2><p>Governance intelligence, conflict checks, and screening oversight.</p></Link>:null;}

"use client";
import Link from "next/link";
import {useEffect,useState} from "react";
import {useRouter} from "next/navigation";
import {developmentEnabled,developmentApiBase,readDevelopmentSession,clearDevelopmentSession,signOutDevelopment,type DevelopmentSession} from "@/lib/developmentSession";
export function DevelopmentSessionBar(){
 const router=useRouter(); const [enabled,setEnabled]=useState(false);const [session,setSession]=useState<DevelopmentSession|null>(null);const [notice,setNotice]=useState("");
 useEffect(()=>{let active=true; const refresh=()=>{if(active)setSession(readDevelopmentSession());};void developmentEnabled().then(e=>{if(active)setEnabled(e);refresh();});
 const timer=setInterval(()=>{const prior=readDevelopmentSession();refresh();if(!prior && session)setNotice("Session expired. Sign in again.");},1000);
 window.addEventListener("development-session-changed",refresh);return()=>{active=false;clearInterval(timer);window.removeEventListener("development-session-changed",refresh);};},[session?.accessToken]);
 useEffect(()=>{if(!session)return;void fetch(`${developmentApiBase()}/api/development-auth/me`,{headers:{Authorization:`Bearer ${session.accessToken}`}}).then(r=>{if(r.status===401){clearDevelopmentSession();setNotice("Session expired or the local API restarted. Sign in again.");}});},[session?.accessToken]);
 if(!enabled)return null;
 return <div aria-label="Development session" style={{padding:"0.7rem",background:"#fff6dc"}}><strong>Development test session</strong>{session?<><span> — {session.user.displayName} · {session.user.role}</span> <Link href={session.user.home}>My assigned work</Link> <small>Expires {new Date(session.expiresAtUtc).toLocaleTimeString()}</small> <button onClick={async()=>{try{await signOutDevelopment();router.push("/sign-in");router.refresh();}catch(e){setNotice(e instanceof Error?e.message:"Sign-out failed");}}}>Sign out</button></>:<> <Link href="/sign-in">Sign in</Link>{notice&&<span role="status"> {notice}</span>}</>}</div>;
}

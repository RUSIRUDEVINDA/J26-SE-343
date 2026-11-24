"use client";
import {SignInLink} from "./SignInLink";
import {useEffect,useState} from "react";
import {useRouter,usePathname} from "next/navigation";
import {applicantComplaintSignIn} from "@/modules/component-04/services/applicantComplaintReturn";
import {developmentEnabled,developmentApiBase,readDevelopmentSession,clearDevelopmentSession,signOutDevelopment,type DevelopmentSession} from "@/lib/developmentSession";
import styles from "./AppHeader.module.css";
export function DevelopmentSessionBar({compact=false}:{compact?:boolean}){
 const router=useRouter(),pathname=usePathname(); const [enabled,setEnabled]=useState(false);const [session,setSession]=useState<DevelopmentSession|null>(null);const [notice,setNotice]=useState("");
 useEffect(()=>{let active=true; const refresh=()=>{if(active)setSession(readDevelopmentSession());};void developmentEnabled().then(e=>{if(active)setEnabled(e);refresh();});
 const timer=setInterval(()=>{const prior=readDevelopmentSession();refresh();if(!prior && session)setNotice("Session expired. Sign in again.");},1000);
 window.addEventListener("development-session-changed",refresh);return()=>{active=false;clearInterval(timer);window.removeEventListener("development-session-changed",refresh);};},[session?.accessToken]);
 useEffect(()=>{if(!session)return;void fetch(`${developmentApiBase()}/api/auth/me`,{headers:{Authorization:`Bearer ${session.accessToken}`}}).then(r=>{if(r.status===401){clearDevelopmentSession();setNotice("Session expired. Sign in again.");}});},[session?.accessToken]);
 if(compact)return <div className={styles.account} aria-label="Account">{session?<><span className={styles.identity}><strong>{session.user.displayName}</strong><small>{session.user.role}</small></span><button className={styles.signIn} onClick={async()=>{try{await signOutDevelopment();router.push(applicantComplaintSignIn(pathname));router.refresh();}catch(e){setNotice(e instanceof Error?e.message:"Sign-out failed");}}}>Sign out</button></>:<SignInLink className={styles.signIn}/>}{notice&&<span role="status">{notice}</span>}</div>;
 if(!enabled)return null;
 return notice?<p role="status">{notice}</p>:null;
}

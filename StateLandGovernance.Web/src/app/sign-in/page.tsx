"use client";
import { useEffect,useState } from "react";
import { useRouter } from "next/navigation";
import { developmentEnabled,signInToPlatform } from "@/lib/developmentSession";
import {c4PostLoginPath} from "@/modules/component-04/services/postLogin";
import styles from "./sign-in.module.css";
import { PageLayout } from "@/shared/components/PageLayout";
export default function SignInPage() {
 const router=useRouter(); const [available,setAvailable]=useState<boolean>(); const [username,setUsername]=useState(""); const [password,setPassword]=useState("");const [error,setError]=useState("");const [busy,setBusy]=useState(false);
 useEffect(()=>{void developmentEnabled().then(setAvailable);},[]);
 return <PageLayout showSidebar={false}><section className={styles.background}><div className={styles.card}><span className={styles.badge}>STATE LAND GOVERNANCE</span><h1>Sign in</h1><p>Enter your username and password to access your workspace.</p>
 {available===false && <p role="alert">Sign-in is currently unavailable. Please try again or contact your administrator.</p>}
 {error && <p role="alert">{error}</p>}
 <form onSubmit={async e=>{e.preventDefault();setBusy(true);setError("");try{let destination="/";const session=await signInToPlatform(username,password,path=>{destination=path;});router.push(c4PostLoginPath(session.user,destination));router.refresh();}catch(e){setError(e instanceof Error?e.message:"Sign-in failed");}finally{setBusy(false);}}}>
 <p><label>Username <input name="username" autoComplete="username" value={username} onChange={e=>setUsername(e.target.value)} required /></label></p>
 <p><label>Password <input name="password" type="password" autoComplete="current-password" value={password} onChange={e=>setPassword(e.target.value)} required /></label></p>
 <button disabled={!available||busy} type="submit">{busy?"Signing in...":"Sign in"}</button></form>
</div></section></PageLayout>;
}

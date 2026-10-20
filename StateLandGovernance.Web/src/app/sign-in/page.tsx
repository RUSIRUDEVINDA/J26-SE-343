"use client";
import { useEffect,useState } from "react";
import { useRouter } from "next/navigation";
import { developmentEnabled,signInDevelopment } from "@/lib/developmentSession";
import styles from "./sign-in.module.css";
import { PageLayout } from "@/shared/components/PageLayout";
export default function SignInPage() {
 const router=useRouter(); const [available,setAvailable]=useState<boolean>(); const [username,setUsername]=useState(""); const [password,setPassword]=useState("");const [error,setError]=useState("");const [busy,setBusy]=useState(false);
 useEffect(()=>{void developmentEnabled().then(setAvailable); const hint=new URLSearchParams(window.location.search).get("account"); if(hint==="research")setUsername(hint);},[]);
 return <PageLayout showSidebar={false}><section className={styles.background}><div className={styles.card}><span className={styles.badge}>LOCAL DEVELOPMENT PORTAL</span><h1>Development test sign-in</h1><p>Local test accounts only. These identities are not production officers or approvals.</p>
 {available===false && <p role="alert">Local Development sign-in is disabled. Start the local API with the explicit Development sign-in opt-in.</p>}
 {error && <p role="alert">{error}</p>}
 <form onSubmit={async e=>{e.preventDefault();setBusy(true);setError("");try{const session=await signInDevelopment(username,password);router.push(session.user.home);router.refresh();}catch(e){setError(e instanceof Error?e.message:"Sign-in failed");}finally{setBusy(false);}}}>
 <p><label>Test username <input name="username" autoComplete="username" value={username} onChange={e=>setUsername(e.target.value)} required /></label></p>
 <p><label>Password <input name="password" type="password" autoComplete="current-password" value={password} onChange={e=>setPassword(e.target.value)} required /></label></p>
 <button disabled={!available||busy} type="submit">{busy?"Signing in...":"Sign in"}</button></form>
 <p>Accounts: applicant, ds, external, provincial, interprovincial, commissioner; research is the separate synthetic screening scenario. Use the documented local test password.</p><details><summary>Test account guide</summary><p>DS: assigned coordinating work. Applicant: owned cases and corrections. External, provincial, interprovincial and commissioner: restricted institutional work.</p><p>Public local test password: Local-Test-2026!</p></details></div></section></PageLayout>;
}

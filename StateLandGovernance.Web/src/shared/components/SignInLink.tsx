"use client";
import Link from "next/link";
import {usePathname,useSearchParams} from "next/navigation";
import {Suspense} from "react";
import {applicantComplaintSignIn} from "@/modules/component-04/services/applicantComplaintReturn";

export function SignInDestinationLink({pathname,returnTo=null,className}:{pathname:string|null;returnTo?:string|null;className?:string}){
 return <Link className={className} href={applicantComplaintSignIn(pathname,returnTo)}>Sign in</Link>;
}
function RouteSignInLink({className}:{className?:string}){
 return <SignInDestinationLink pathname={usePathname()} returnTo={useSearchParams()?.get("returnTo")??null} className={className}/>;
}
export function SignInLink({className}:{className?:string}){
 return <Suspense fallback={<span className={className} aria-busy="true">Loading sign-in link…</span>}><RouteSignInLink className={className}/></Suspense>;
}

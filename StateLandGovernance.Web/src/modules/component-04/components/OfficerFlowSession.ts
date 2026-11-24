"use client";
import {useEffect,useRef,useState} from "react";
import {readDevelopmentSession} from "@/lib/developmentSession";
export function useOfficerSession(){
 const [session,setSession]=useState({actor:"",generation:0});const epoch=useRef(0);
 useEffect(()=>{const reset=()=>{epoch.current++;setSession({actor:readDevelopmentSession()?.user.actorId??"",generation:epoch.current});};queueMicrotask(reset);window.addEventListener("development-session-changed",reset);return()=>{epoch.current++;window.removeEventListener("development-session-changed",reset);};},[]);
 return {...session,epoch};
}

"use client";
import {useEffect} from "react";
import {investigationLeaveEvent} from "../services/investigationLeave";
export function useInvestigationLeaveGuard(dirty:boolean){
 useEffect(()=>{
  if(!dirty)return;
  const currentUrl=window.location.href;
  const confirm=()=>window.confirm("Discard unsaved investigation changes and leave this draft?");
  const leave=(event:Event)=>{if(!confirm())event.preventDefault();};
  const unload=(event:BeforeUnloadEvent)=>{event.preventDefault();event.returnValue="";};
  const link=(event:MouseEvent)=>{const anchor=event.target instanceof Element?event.target.closest("a[href]"):null;if(anchor instanceof HTMLAnchorElement&&anchor.href!==window.location.href&&!confirm()){event.preventDefault();event.stopImmediatePropagation();}};
  const back=()=>{if(!confirm())window.history.pushState(null,"",currentUrl);};
  window.addEventListener(investigationLeaveEvent,leave);window.addEventListener("beforeunload",unload);document.addEventListener("click",link,true);window.addEventListener("popstate",back);
  return()=>{window.removeEventListener(investigationLeaveEvent,leave);window.removeEventListener("beforeunload",unload);document.removeEventListener("click",link,true);window.removeEventListener("popstate",back);};
 },[dirty]);
}

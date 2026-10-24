"use client";
import {useEffect,useRef,type ReactNode} from "react";
import styles from "./IndependentGovernance.module.css";
export function ReportDialog({onClose,children}:{onClose:()=>void;children:ReactNode}) {
 const ref=useRef<HTMLDialogElement>(null);
 useEffect(()=>{const dialog=ref.current;dialog?.showModal();return()=>dialog?.close();},[]);
 return <dialog ref={ref} className={styles.drawer} aria-label="Historical assessment details" onCancel={e=>{e.preventDefault();onClose();}}><button autoFocus className={styles.close} onClick={onClose}>Close historical assessment</button>{children}</dialog>;
}

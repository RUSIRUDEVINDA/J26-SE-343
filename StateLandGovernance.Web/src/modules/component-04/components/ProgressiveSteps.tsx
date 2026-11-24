import type {ReactNode} from "react";
import {statusLabel,statusTone,type StatusTone} from "../services/presentation";
import styles from "./ProgressiveSteps.module.css";
export function StatusBadge({status,tone}:{status:string;tone?:StatusTone}){return <span className={styles.badge} data-tone={tone??statusTone(status)}>{statusLabel(status)}</span>;}
export function ProgressiveSteps({children,label}:{children?:ReactNode;label:string}){return <ol className={styles.steps} aria-label={label}>{children}</ol>;}
export function ProgressiveStep({title,description,status,tone,children,open=false,current=false,expandable=true}:{title:ReactNode;description?:ReactNode;status:string;tone?:StatusTone;children?:ReactNode;open?:boolean;current?:boolean;expandable?:boolean}){
 const row=<><span className={styles.indicator} aria-hidden="true"/><span className={styles.copy}><span className={styles.title}>{title}</span>{description&&<span className={styles.description}>{description}</span>}</span><StatusBadge status={status} tone={tone}/></>;
 return <li className={styles.step} data-tone={tone??statusTone(status)} data-current={current||undefined}>{children&&expandable?<details open={open||undefined}><summary className={styles.row}>{row}</summary><div className={styles.detail}>{children}</div></details>:<><div className={styles.row}>{row}</div>{children&&<div className={styles.detail}>{children}</div>}</>}</li>;
}

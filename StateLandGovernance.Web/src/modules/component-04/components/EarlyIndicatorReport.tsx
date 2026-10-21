"use client";
import {useState} from "react";
import styles from "./AssessmentReport.module.css";
type Indicator={indicatorType:string;evidenceState:string;requiresReview:boolean;needsEvidence:boolean;message:string;evidenceReference?:string};
export function EarlyIndicatorReport({items}:{items:Indicator[]}){
 const [index,setIndex]=useState(0);const selected=items[index]||items[0];
 if(!selected)return null;
 return <div className={styles.report}><section className={styles.checks}><h2>Evaluated governance indicators</h2>{items.map((f,i)=><button key={f.indicatorType} className={styles.step} aria-pressed={f===selected} onClick={()=>setIndex(i)}><span aria-hidden="true" className={`${styles.circle} ${f.needsEvidence||f.requiresReview?styles.warning:styles.success}`}>{f.needsEvidence?"?":f.requiresReview?"!":"✓"}</span><span className={styles.stepText}><strong>{f.indicatorType.replace(/([A-Z])/g," $1").trim()}</strong><small>{f.evidenceState.replace(/([A-Z])/g," $1").trim()}</small></span></button>)}</section><section className={styles.detail}><h2>{selected.indicatorType.replace(/([A-Z])/g," $1").trim()}</h2><h3>What was found?</h3><p>{selected.message}</p><h3>Evidence</h3><p>{selected.evidenceReference||"No evidence reference returned"}</p><h3>What happens next?</h3><p>{selected.needsEvidence?"Supply reviewed evidence before relying on this indicator.":selected.requiresReview?"Authorized officer review is required.":"Retain the returned finding for review. It is not application approval."}</p></section></div>;
}

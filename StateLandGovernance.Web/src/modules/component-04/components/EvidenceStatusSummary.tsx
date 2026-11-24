import styles from "./EvidenceStatusSummary.module.css";
export function EvidenceStatusSummary({states,label="Recorded screening evidence"}:{states:readonly string[];label?:string}){
 const verified=states.filter(s=>s==="VerifiedPresent"||s==="VerifiedAbsent").length;
 const counts=[{label:"Missing",count:states.filter(s=>s==="Missing").length},{label:"Unverified",count:states.filter(s=>s==="Unverified").length},{label:"Verified",count:verified}];
 const other=states.length-counts.reduce((n,x)=>n+x.count,0);
 return <section aria-label={label}><h4>{label}</h4><dl className={styles.counts}>{counts.map(x=><div key={x.label}><dt>{x.label}</dt><dd>{x.count}</dd></div>)}{other>0&&<div><dt>Other recorded states</dt><dd>{other}</dd></div>}</dl><details><summary>What the evidence statuses mean</summary><dl><dt>Missing</dt><dd>Required evidence has not been provided.</dd><dt>Unverified</dt><dd>Information exists, but an authorized officer has not verified the supporting evidence.</dd><dt>Verified</dt><dd>An authorized DS officer has reviewed and accepted the specified evidence for the recorded verification purpose.</dd></dl><p>Missing and Unverified do not establish wrongdoing. Verified does not constitute legal approval. Original automated screening states remain separate from any later manual verification history; saving an observation does not verify it.</p></details></section>;
}
